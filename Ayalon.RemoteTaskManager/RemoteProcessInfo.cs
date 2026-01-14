using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Windows.Data;

namespace Ayalon.RemoteTaskManager
{
    public class RemoteTaskManager
    {
        // ייצוג של תהליך ביישום שלך (כדי להציג ב-DataGrid)
        public class RemoteProcessInfo
        {
            public uint ProcessId { get; set; }
            public string Name { get; set; }
            public long MemoryUsageBytes { get; set; }
            public int ThreadCount { get; set; }
            public string Path { get; set; }
            public ManagementObject WmiObject { get; private set; } // לשמירת ההפניה ל-WMI
            public bool IsFilteredMatch { get; set; }

            public RemoteProcessInfo(ManagementObject mo)
            {
                WmiObject = mo;
                ProcessId = (uint)mo["ProcessId"];
                Name = (string)mo["Name"];
                // WorkingSetSize הוא בבתים, נשמור אותו כ-long
                MemoryUsageBytes = (long)(ulong)mo["WorkingSetSize"];
                ThreadCount = (int)(uint)mo["ThreadCount"];
                Path = mo["ExecutablePath"] as string;
            }
        }

        private static ConnectionOptions GetDefaultOptions()
        {
            return new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
                EnablePrivileges = true,
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public static List<RemoteProcessInfo> GetProcesses(string computerName)
        {
            List<RemoteProcessInfo> processList = new List<RemoteProcessInfo>();

            try
            {
                // יצירת סקופ החיבור (הכתובת למחשב המרוחק)
                ManagementScope scope = new ManagementScope($@"\\{computerName}\root\cimv2", GetDefaultOptions());
                scope.Connect(); // ניסיון חיבור (יזרוק Exception אם נכשל)

                // השאילתה לשליפת מידע התהליכים
                ObjectQuery query = new ObjectQuery("SELECT ProcessId, Name, WorkingSetSize, ThreadCount, ExecutablePath FROM Win32_Process");

                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);

                // מעבר על כל התהליכים והוספה לרשימה
                foreach (ManagementObject mo in searcher.Get())
                {
                    processList.Add(new RemoteProcessInfo(mo));
                }
            }
            catch (ManagementException ex)
            {
                // טיפול בשגיאות WMI ספציפיות (כמו "Access Denied" או "RPC server is unavailable")
                throw new Exception($"WMI Error while connecting or querying: {ex.Message}");
            }
            catch (Exception ex)
            {
                // טיפול בשגיאות כלליות
                throw new Exception($"General error: {ex.Message}");
            }

            return processList;
        }

        // מתודה לסיום תהליך
        public static void TerminateProcess(RemoteProcessInfo process)
        {
            // הפעלת המתודה Terminate() על אובייקט ה-WMI שנשמר
            process.WmiObject.InvokeMethod("Terminate", null);
        }

        public class SystemPerformance
        {
            public double CpuUsagePercent { get; set; }
            public double FreeMemoryMB { get; set; }

            public static SystemPerformance GetSystemPerformance(string computerName)
            {
                ManagementScope scope = new ManagementScope($@"\\{computerName}\root\cimv2", GetDefaultOptions());
                scope.Connect();

                // אובייקט לאחסון הנתונים
                var performanceData = new SystemPerformance();

                // 1. קבלת נתוני מעבד
                // ה-Win32_PerfFormattedData_PerfOS_Processor לרוב מחזיר מספר מופעים,
                // נחפש את InstanceName='_Total'
                ObjectQuery cpuQuery = new ObjectQuery("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
                ManagementObjectSearcher cpuSearcher = new ManagementObjectSearcher(scope, cpuQuery);

                foreach (ManagementObject mo in cpuSearcher.Get())
                {
                    // PercentProcessorTime הוא בפורמט string או ulong, ממיר ל-double
                    performanceData.CpuUsagePercent = Convert.ToDouble(mo["PercentProcessorTime"]);
                    break; // יציאה לאחר ה-Total
                }

                // 2. קבלת נתוני זיכרון פנוי
                // Win32_OperatingSystem מכיל נתונים כלליים, כולל FreePhysicalMemory ב-KB
                ObjectQuery memQuery = new ObjectQuery("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
                ManagementObjectSearcher memSearcher = new ManagementObjectSearcher(scope, memQuery);

                foreach (ManagementObject mo in memSearcher.Get())
                {
                    ulong freeKB = (ulong)mo["FreePhysicalMemory"];
                    // המרה מ-KB ל-MB (חלוקה ב-1024)
                    performanceData.FreeMemoryMB = (double)freeKB / 1024.0;
                    break;
                }

                return performanceData;
            }
        }
    }
}