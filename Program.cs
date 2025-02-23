using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

class Program
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

    const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4; // Marks file for deletion at reboot

    static void Main()
    {
        string backupDirectory = @"C:\Windows\System32\config\backup";
        string hiveDirectory = @"C:\Windows\System32\config"; // Target system hives location
        string hiveName = "SAM"; // We are only replacing the SAM hive

        try
        {
            // Ensure backup directory exists
            if (!Directory.Exists(backupDirectory))
            {
                Console.WriteLine("Backup directory not found!");
                return;
            }

            // Ensure SAM backup file exists
            string backupPath = Path.Combine(backupDirectory, hiveName);
            if (!File.Exists(backupPath))
            {
                Console.WriteLine("Missing backup file: " + backupPath);
                return;
            }

            Console.WriteLine("Backup file found. Preparing registry update...");

            // Step 1: Schedule original SAM hive deletion
            string originalPath = Path.Combine(hiveDirectory, hiveName);
            bool success = MoveFileEx(originalPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
            if (!success)
            {
                Console.WriteLine($"Failed to mark {originalPath} for deletion.");
            }
            else
            {
                Console.WriteLine($"Scheduled deletion for {originalPath}.");
            }

            // Step 2: Schedule SAM hive for replacement after reboot
            string[] pendingOperations = new[]
            {
                @"\??\" + backupPath,    // New file from backup
                @"\??\" + originalPath   // Target file to replace
            };

            using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager", writable: true))
            {
                if (regKey == null)
                {
                    Console.WriteLine("Failed to open registry key.");
                    return;
                }

                object existingValue = regKey.GetValue("PendingFileRenameOperations");
                string[] existingEntries = existingValue as string[] ?? new string[0];

                // Merge with existing operations
                string[] updatedEntries = existingEntries.Concat(pendingOperations).ToArray();

                regKey.SetValue("PendingFileRenameOperations", updatedEntries, RegistryValueKind.MultiString);
                Console.WriteLine("Registry hive restore scheduled. Restart required.");
            }

            // Verify changes
            using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager"))
            {
                object updatedValue = regKey?.GetValue("PendingFileRenameOperations");
                if (updatedValue is string[] updatedEntries)
                {
                    Console.WriteLine("Updated PendingFileRenameOperations:");
                    foreach (var entry in updatedEntries)
                    {
                        Console.WriteLine(entry);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to verify registry update.");
                }
            }

            Console.WriteLine("Restart your system to complete the process.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
