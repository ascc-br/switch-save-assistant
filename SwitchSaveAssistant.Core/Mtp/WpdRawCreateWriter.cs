using System.Reflection;
using System.Runtime.ExceptionServices;
using MediaDevices;

namespace SwitchSaveAssistant.Core.Mtp;

internal sealed class WpdRawCreateWriter
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    public void CreateEvenIfNameExists(
        MediaDevice device,
        string localPath,
        string remotePath
    )
    {
        ArgumentNullException.ThrowIfNull(
            device
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            localPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            remotePath
        );

        if (!File.Exists(
                localPath
            ))
        {
            throw new FileNotFoundException(
                "Arquivo local não encontrado.",
                localPath
            );
        }

        var normalizedRemote =
            remotePath
                .Replace(
                    '/',
                    '\\'
                )
                .TrimEnd(
                    '\\'
                );

        var separator =
            normalizedRemote.LastIndexOf(
                '\\'
            );

        if (separator <= 0 ||
            separator ==
            normalizedRemote.Length - 1)
        {
            throw new ArgumentException(
                $"Caminho remoto inválido: '{remotePath}'.",
                nameof(remotePath)
            );
        }

        var remoteDirectory =
            normalizedRemote[..separator];

        var fileName =
            normalizedRemote[(separator + 1)..];

        var directoryInfo =
            device.GetDirectoryInfo(
                remoteDirectory
            );

        var baseType =
            typeof(MediaFileSystemInfo);

        var workerField =
            RequireField(
                baseType,
                "mainWorker"
            );

        var itemField =
            RequireField(
                baseType,
                "item"
            );

        var worker =
            workerField.GetValue(
                directoryInfo
            )
            ?? throw new InvalidOperationException(
                "Worker interno do MediaDevices não disponível."
            );

        var item =
            itemField.GetValue(
                directoryInfo
            )
            ?? throw new InvalidOperationException(
                "Item WPD interno do diretório não disponível."
            );

        var uploadMethod =
            item.GetType()
                .GetMethods(
                    InstanceFlags
                )
                .Single(
                    method =>
                        method.Name ==
                        "UploadFile" &&
                        method.GetParameters().Length ==
                        5 &&
                        method.GetParameters()[0]
                            .ParameterType ==
                        typeof(string) &&
                        typeof(Stream).IsAssignableFrom(
                            method.GetParameters()[1]
                                .ParameterType
                        )
                );

        var invokeMethod =
            worker.GetType()
                .GetMethods(
                    InstanceFlags
                )
                .Single(
                    method =>
                        method.Name ==
                        "Invoke" &&
                        !method.IsGenericMethodDefinition &&
                        method.GetParameters().Length ==
                        4 &&
                        method.GetParameters()[0]
                            .ParameterType ==
                        typeof(Action)
                );

        Action action =
            () =>
            {
                using var source =
                    File.OpenRead(
                        localPath
                    );

                var now =
                    DateTime.Now;

                InvokeUnwrapped(
                    uploadMethod,
                    item,
                    new object?[]
                    {
                        fileName,
                        source,
                        now,
                        File.GetLastWriteTime(
                            localPath
                        ),
                        File.GetLastWriteTime(
                            localPath
                        )
                    }
                );
            };

        InvokeUnwrapped(
            invokeMethod,
            worker,
            new object?[]
            {
                action,
                0,
                string.Empty,
                nameof(CreateEvenIfNameExists)
            }
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
}
