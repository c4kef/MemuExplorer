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

        public static async Task<string> GetAsync(string url, int timeout = 10)
        {
            HttpClient request = new HttpClient();

            request.Timeout = TimeSpan.FromSeconds(timeout);

            //request.DefaultRequestHeaders.Add("Authorization", "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczpcL1wvbWlrYXdpc2UuY29tXC9ydV9ydVwvIiwiaWF0IjoxNjY2NDU2NjIyLCJuYmYiOjE2NjY0NTY2MjIsImV4cCI6MTY2NzA2MTQyMiwiZGF0YSI6eyJ1c2VyIjp7ImlkIjoiNzIifX19.1SCeDJf9rBWAfid5aAXt7NK7PMKnagc2Z0jtfDp7usI");
            request.DefaultRequestHeaders.UserAgent.ParseAdd(@"Mozilla/5.0 (Windows; Windows NT 6.1) AppleWebKit/534.23 (KHTML, like Gecko) Chrome/11.0.686.3 Safari/534.23");

            HttpResponseMessage response = await request.GetAsync(url);

            //Bypass UTF8 error encoding
            byte[] buf = await response.Content.ReadAsByteArrayAsync();
            return Encoding.UTF8.GetString(buf);
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
