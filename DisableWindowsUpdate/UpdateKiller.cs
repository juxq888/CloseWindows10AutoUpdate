using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.ServiceProcess;
using System.Threading;

namespace DisableWindowsUpdate
{
    public class UpdateKiller
    {
        private static readonly int DISABLE = 0x04;
        private static readonly int AUTO = 0x02;

        private static readonly byte[] WIN_UPDATE_FAILURE_ACTIONS_VALUE = new byte[44]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x60, 0xEA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x60, 0xEA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x60, 0xEA, 0x00, 0x00
        };

        public bool CloseService()
        {
            RegistryKey winUpdateKey = null;
            try
            {
                Console.WriteLine("Search Service...");
                var scServices = ServiceController.GetServices();
                ServiceController winUpdateService = null;
                foreach (var service in scServices)
                {
                    if (service.DisplayName == "Windows Update")
                    {
                        winUpdateService = service;
                        break;
                    }
                }
                if (winUpdateService == null)
                {
                    throw new Exception("can not find Windows Update service");
                }

                if (winUpdateService.Status == ServiceControllerStatus.Running)
                {
                    Console.WriteLine("Windows Update Service is Running, Closing...");
                    winUpdateService.Stop();
                    while (winUpdateService.Status != ServiceControllerStatus.Stopped)
                    {
                        Thread.Sleep(1000);
                        winUpdateService.Refresh();
                    }
                    Console.WriteLine("Done");
                }

                winUpdateKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv", true);
                Console.WriteLine("Disable Windows Update Service...");
                winUpdateKey.SetValue("Start", DISABLE, RegistryValueKind.DWord);
                winUpdateKey.SetValue("FailureActions", WIN_UPDATE_FAILURE_ACTIONS_VALUE, RegistryValueKind.Binary);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failure: {e.Message}, Exit");
                return false;
            }
            finally
            {
                winUpdateKey.Close();
            }

            Console.WriteLine("Success");
            return true;
        }

        public bool SetPolicy()
        {
            RegistryKey groupPolicyRegistry = null;
            RegistryKey winUpdateKey = null;
            RegistryKey winAutoUpdateKey = null;
            try
            {
                groupPolicyRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows", true);
                winUpdateKey = groupPolicyRegistry.CreateSubKey("WindowsUpdate", true);
                winUpdateKey.SetValue("SetDisableUXWUAccess", 0x01, RegistryValueKind.DWord);

                winAutoUpdateKey = winUpdateKey.CreateSubKey("AU", true);
                winAutoUpdateKey.SetValue("NoAutoUpdate", 0x01, RegistryValueKind.DWord);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failure: {e.Message}, Exit");
                return false;
            }
            finally
            {
                winAutoUpdateKey?.Close();
                winUpdateKey?.Close();
                groupPolicyRegistry?.Close();
            }

            Console.WriteLine("Success");
            return true;
        }

        public bool KillTaskScheduler()
        {
            using (TaskService ts = new TaskService())
            {
                TaskFolder folder = ts.GetFolder(@"MicroSoft\Windows\WindowsUpdate");
                foreach (var task in folder.Tasks)
                {
                    try
                    {
                        if (task.Definition.Settings.Enabled != false)
                        {
                            task.Definition.Settings.Enabled = false;
                            task.RegisterChanges();
                            Console.WriteLine($"Diable Task {task.Name} Success");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Diable Task {task.Name} Fail, {e.Message}");
                        continue;
                    }
                }
            }

            Console.WriteLine("Success");
            return true;
        }

        public bool SetRegistry()
        {
            RegistryKey key = null;
            try
            {
                key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\UsoSvc", true);
                key.SetValue("Start", DISABLE, RegistryValueKind.DWord);
                byte[] currentActions = (byte[])key.GetValue("FailureActions");
                currentActions[20] = 0;
                currentActions[28] = 0;
                key.SetValue("FailureActions", currentActions, RegistryValueKind.Binary);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                if (key != null) key.Close();
            }

            Console.WriteLine("Success");
            return true;
        }

        public void ResumeUpdate()
        {
            var winUpdateKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv", true);
            Console.WriteLine("Resume Windows Update Service...");
            winUpdateKey.SetValue("Start", AUTO, RegistryValueKind.DWord);
            if (winUpdateKey.GetValue("FailureActions") != null)
            {
                winUpdateKey.DeleteValue("FailureActions");
            }

            var groupPolicyRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows", true);
            var keys = groupPolicyRegistry.GetSubKeyNames();
            foreach (var key in keys)
            {
                if (key == "WindowsUpdate")
                {
                    groupPolicyRegistry.DeleteSubKeyTree(key);
                    break;
                }
            }

            using (TaskService ts = new TaskService())
            {
                TaskFolder folder = ts.GetFolder(@"MicroSoft\Windows\WindowsUpdate");
                foreach (var task in folder.Tasks)
                {
                    try
                    {
                        if (task.Definition.Settings.Enabled == false)
                        {
                            task.Definition.Settings.Enabled = true;
                            task.RegisterChanges();
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            winUpdateKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\UsoSvc", true);
            winUpdateKey.SetValue("Start", AUTO, RegistryValueKind.DWord);
            byte[] currentActions = (byte[])winUpdateKey.GetValue("FailureActions");
            currentActions[20] = 0x01;
            currentActions[28] = 0x01;
            winUpdateKey.SetValue("FailureActions", currentActions, RegistryValueKind.Binary);
        }
    }
}