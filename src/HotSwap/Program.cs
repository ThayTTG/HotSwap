namespace HotSwap;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Impede duas instâncias rodando ao mesmo tempo (ex: se a Tarefa Agendada
        // disparar de novo enquanto o app já está na bandeja).
        using var singleInstanceMutex = new Mutex(true, "HotSwap_SingleInstance_Mutex", out bool isNew);
        if (!isNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
