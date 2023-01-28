using System;

namespace DisableWindowsUpdate
{
    class Program
    {
        private static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                CloseUpdate();
            }

            switch (args[0])
            {
                case "Close":
                    CloseUpdate();
                    break;
                case "Open":
                    OpenUpdate();
                    break;
            }

            Console.WriteLine("Press anykey to quit...");
            Console.ReadKey();
        }

        private static void CloseUpdate()
        {
            Console.WriteLine("Close Windows Update\n");
            UpdateKiller uk = new UpdateKiller();

            if (!uk.CloseService()) return;
            if (!uk.SetPolicy()) return;
            if (!uk.KillTaskScheduler()) return;
            if (!uk.SetRegistry()) return;
        }

        private static void OpenUpdate()
        {
            Console.WriteLine("Open Windows Update\n");
            UpdateKiller uk = new UpdateKiller();

            uk.ResumeUpdate();
        }

    }
}
