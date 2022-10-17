using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBot.Controls
{
    public static class ResourceHelper
    {
        public static Bitmap Base64StringToBitmap(this string base64String)
        {
            byte[] byteBuffer = Convert.FromBase64String(base64String);
            MemoryStream memoryStream = new(byteBuffer)
            {
                Position = 0
            };

            Bitmap bmpReturn = (Bitmap)System.Drawing.Image.FromStream(memoryStream);
            memoryStream.Close();

            return bmpReturn;
        }

        public static object FindResource(this VisualElement o, string key)
        {
            while (o != null)
            {
                if (o.Resources.TryGetValue(key, out var r1)) return r1;
                if (o is Page) break;
                if (o is IElement e) o = e.Parent as VisualElement;
            }
            if (Application.Current.Resources.TryGetValue(key, out var r2)) return r2;
            return null;
        }


        public static void Sort<T>(this ObservableCollection<T> collection, Comparison<T> comparison)
        {
            var sortableList = new List<T>(collection);
            sortableList.Sort(comparison);

            for (int i = 0; i < sortableList.Count; i++)
            {
                collection.Move(collection.IndexOf(sortableList[i]), i);
            }
        }
    }
}
