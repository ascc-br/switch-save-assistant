using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using MediaDevices;

namespace SwitchSaveAssistant.Core.Mtp;

internal sealed class WpdExistingFileWriter
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private const BindingFlags StaticFlags =
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    public void Overwrite(
        MediaDevice device,
        string localPath,
        string remotePath
    )
    {
        ArgumentNullException.ThrowIfNull(device);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            localPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            remotePath
        );

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException(
                "Arquivo local não encontrado.",
                localPath
            );
        }

        var remoteFile =
            device.GetFileInfo(
                remotePath
            );

        var assembly =
            typeof(MediaDevice).Assembly;

        var workerField =
            RequireField(
                typeof(MediaDevice),
                "mainWorker"
            );

        var contentField =
            RequireField(
                typeof(MediaDevice),
                "deviceContent"
            );

        var worker =
            workerField.GetValue(device)
            ?? throw new InvalidOperationException(
                "Worker interno do MediaDevices não disponível."
            );

        var content =
            contentField.GetValue(device)
            ?? throw new InvalidOperationException(
                "Conteúdo WPD do dispositivo não disponível."
            );

        var invokeMethod =
            worker.GetType()
                .GetMethods(InstanceFlags)
                .Single(method =>
                    method.Name == "Invoke" &&
                    !method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 4 &&
                    method.GetParameters()[0].ParameterType ==
                        typeof(Action)
                );

        Action action = () =>
            UpdateOnWorker(
                assembly,
                content,
                remoteFile.Id,
                remoteFile.Name,
                localPath
            );

        InvokeUnwrapped(
            invokeMethod,
            worker,
            new object?[]
            {
                action,
                0,
                string.Empty,
                nameof(Overwrite)
            }
        );
    }

    private static void UpdateOnWorker(
        Assembly assembly,
        object content,
        string objectId,
        string remoteName,
        string localPath
    )
    {
        object? properties = null;
        object? stream = null;

        try
        {
            var content2Type =
                RequireType(
                    assembly,
                    "MediaDevices.Internal.IPortableDeviceContent2"
                );

            if (!content2Type.IsInstanceOfType(content))
            {
                throw new NotSupportedException(
                    "O dispositivo não expôs IPortableDeviceContent2."
                );
            }

            using var source =
                File.OpenRead(
                    localPath
                );

            properties =
                CreateUpdateProperties(
                    assembly,
                    (ulong)source.Length,
                    remoteName
                );

            var updateMethod =
                RequireMethod(
                    content2Type,
                    "UpdateObjectWithPropertiesAndData"
                );

            var updateArgs =
                new object?[]
                {
                    objectId,
                    properties,
                    null,
                    (uint)0
                };

            InvokeUnwrapped(
                updateMethod,
                content,
                updateArgs
            );

            stream =
                updateArgs[2]
                ?? throw new InvalidOperationException(
                    "WPD não retornou stream para atualização."
                );

            var optimalBufferSize =
                Convert.ToUInt32(
                    updateArgs[3]
                    ?? 0u
                );

            var streamInterface =
                RequireType(
                    assembly,
                    "MediaDevices.Interfaces.IStream"
                );

            var writeMethod =
                RequireMethod(
                    streamInterface,
                    "Write"
                );

            var commitMethod =
                RequireMethod(
                    streamInterface,
                    "Commit"
                );

            var buffer =
                new byte[
                    ResolveBufferSize(
                        optimalBufferSize
                    )
                ];

            while (true)
            {
                var bytesRead =
                    source.Read(
                        buffer,
                        0,
                        buffer.Length
                    );

                if (bytesRead == 0)
                {
                    break;
                }

                var offset = 0;

                while (offset < bytesRead)
                {
                    var count =
                        bytesRead - offset;

                    byte[] chunk;

                    if (offset == 0 &&
                        count == buffer.Length)
                    {
                        chunk = buffer;
                    }
                    else
                    {
                        chunk =
                            new byte[count];

                        Buffer.BlockCopy(
                            buffer,
                            offset,
                            chunk,
                            0,
                            count
                        );
                    }

                    var writeArgs =
                        new object?[]
                        {
                            chunk,
                            count,
                            (ulong)0
                        };

                    var hr =
                        InvokeHResult(
                            writeMethod,
                            stream,
                            writeArgs
                        );

                    ThrowIfFailed(
                        hr,
                        "IStream.Write"
                    );

                    var written =
                        Convert.ToUInt64(
                            writeArgs[2]
                            ?? 0UL
                        );

                    if (written == 0 ||
                        written > (ulong)count)
                    {
                        throw new IOException(
                            $"Quantidade inválida gravada pelo WPD: {written}."
                        );
                    }

                    offset += checked(
                        (int)written
                    );
                }
            }

            var commitHr =
                InvokeHResult(
                    commitMethod,
                    stream,
                    new object?[]
                    {
                        0
                    }
                );

            ThrowIfFailed(
                commitHr,
                "IStream.Commit"
            );
        }
        catch
        {
            if (stream is not null)
            {
                try
                {
                    var streamInterface =
                        RequireType(
                            assembly,
                            "MediaDevices.Interfaces.IStream"
                        );

                    var revertMethod =
                        RequireMethod(
                            streamInterface,
                            "Revert"
                        );

                    InvokeUnwrapped(
                        revertMethod,
                        stream,
                        Array.Empty<object?>()
                    );
                }
                catch
                {
                    // Preserva a exceção original.
                }
            }

            throw;
        }
        finally
        {
            TryFinalRelease(stream);
            TryFinalRelease(properties);
        }
    }

    private static object CreateUpdateProperties(
        Assembly assembly,
        ulong length,
        string remoteName
    )
    {
        var valuesType =
            RequireType(
                assembly,
                "MediaDevices.Internal.IPortableDeviceValues"
            );

        var comHelperType =
            RequireType(
                assembly,
                "MediaDevices.Internal.ComHelper"
            );

        var createInstance =
            comHelperType
                .GetMethods(StaticFlags)
                .Single(method =>
                    method.Name == "CreateInstance" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1
                )
                .MakeGenericMethod(
                    valuesType
                );

        var properties =
            InvokeUnwrapped(
                createInstance,
                null,
                new object?[]
                {
                    0,
                    string.Empty,
                    nameof(CreateUpdateProperties)
                }
            )
            ?? throw new InvalidOperationException(
                "Não foi possível criar IPortableDeviceValues."
            );

        var wpdType =
            RequireType(
                assembly,
                "MediaDevices.Internal.WPD"
            );

        var objectSize =
            RequireStaticFieldValue(
                wpdType,
                "OBJECT_SIZE"
            );

        var originalFileName =
            RequireStaticFieldValue(
                wpdType,
                "OBJECT_ORIGINAL_FILE_NAME"
            );

        var objectName =
            RequireStaticFieldValue(
                wpdType,
                "OBJECT_NAME"
            );

        SetProperty(
            valuesType,
            properties,
            "SetUnsignedLargeIntegerValue",
            new object?[]
            {
                objectSize,
                length
            },
            "WPD_OBJECT_SIZE"
        );

        SetProperty(
            valuesType,
            properties,
            "SetStringValue",
            new object?[]
            {
                originalFileName,
                remoteName
            },
            "WPD_OBJECT_ORIGINAL_FILE_NAME"
        );

        SetProperty(
            valuesType,
            properties,
            "SetStringValue",
            new object?[]
            {
                objectName,
                remoteName
            },
            "WPD_OBJECT_NAME"
        );

        return properties;
    }

    private static void SetProperty(
        Type valuesType,
        object properties,
        string methodName,
        object?[] args,
        string propertyName
    )
    {
        var method =
            RequireMethod(
                valuesType,
                methodName
            );

        var hr =
            InvokeHResult(
                method,
                properties,
                args
            );

        ThrowIfFailed(
            hr,
            propertyName
        );
    }

    private static int ResolveBufferSize(
        uint optimal
    )
    {
        if (optimal == 0)
        {
            return 64 * 1024;
        }

        return checked(
            (int)Math.Min(
                optimal,
                1024u * 1024u
            )
        );
    }

    private static object RequireStaticFieldValue(
        Type type,
        string name
    )
    {
        var field =
            type.GetField(
                name,
                StaticFlags
            )
            ?? throw new MissingFieldException(
                type.FullName,
                name
            );

        return field.GetValue(null)
            ?? throw new InvalidOperationException(
                $"Campo '{name}' não possui valor."
            );
    }

    private static Type RequireType(
        Assembly assembly,
        string name
    )
    {
        return assembly.GetType(
                   name,
                   throwOnError: false
               )
               ?? throw new TypeLoadException(
                   $"Tipo interno não encontrado: {name}"
               );
    }

    private static FieldInfo RequireField(
        Type type,
        string name
    )
    {
        return type.GetField(
                   name,
                   InstanceFlags
               )
               ?? throw new MissingFieldException(
                   type.FullName,
                   name
               );
    }

    private static MethodInfo RequireMethod(
        Type type,
        string name
    )
    {
        return type
                   .GetMethods(
                       InstanceFlags
                   )
                   .FirstOrDefault(
                       method =>
                           method.Name == name
                   )
               ?? throw new MissingMethodException(
                   type.FullName,
                   name
               );
    }

    private static int InvokeHResult(
        MethodInfo method,
        object target,
        object?[] args
    )
    {
        var result =
            InvokeUnwrapped(
                method,
                target,
                args
            );

        return result is int hr
            ? hr
            : throw new InvalidOperationException(
                $"{method.Name} não retornou HRESULT."
            );
    }

    private static object? InvokeUnwrapped(
        MethodInfo method,
        object? target,
        object?[] args
    )
    {
        try
        {
            return method.Invoke(
                target,
                args
            );
        }
        catch (TargetInvocationException ex)
            when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo
                .Capture(
                    ex.InnerException
                )
                .Throw();

            throw;
        }
    }

    private static void ThrowIfFailed(
        int hr,
        string operation
    )
    {
        if (hr >= 0)
        {
            return;
        }

        var description =
            Marshal.GetExceptionForHR(
                hr
            )?.Message
            ?? "erro COM desconhecido";

        throw new IOException(
            $"{operation} falhou. " +
            $"HRESULT=0x{hr:X8}. " +
            $"{description}"
        );
    }

    private static void TryFinalRelease(
        object? value
    )
    {
        if (value is null)
        {
            return;
        }

        try
        {
            var finalRelease =
                value.GetType()
                    .GetMethod(
                        "FinalRelease",
                        InstanceFlags
                    );

            finalRelease?.Invoke(
                value,
                null
            );
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
