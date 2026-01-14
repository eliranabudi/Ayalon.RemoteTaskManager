using System;
using System.Globalization;
using System.Windows.Data;

namespace Ayalon.RemoteTaskManager // שינוי כאן: מרחב השמות
{
    // מחלקה זו ממירה ערכי Bytes ל-String (MB)
    public class BytesToMBConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ulong bytes)
            {
                double megabytes = (double)bytes / 1024.0 / 1024.0;
                return $"{megabytes:N1}";
            }
            if (value is long longBytes)
            {
                double megabytes = (double)longBytes / 1024.0 / 1024.0;
                return $"{megabytes:N1}";
            }
            return "N/A";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
