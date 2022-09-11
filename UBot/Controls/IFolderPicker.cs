using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBot.Controls
{
    public interface IFolderPicker
    {
        Task<string> PickFolder();
        Task<string> PickFile(string filter);
    }
}
