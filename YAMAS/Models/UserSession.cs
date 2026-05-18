using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YAMAS.Models
{
    /// <summary>
    /// Holds current session state shared across all views.
    /// Acts as a lightweight singleton so ROM data persists
    /// without passing it through every constructor.
    /// </summary>
    public static class UserSession
    {
        public static UserROMProfile ROMProfile { get; set; } = new UserROMProfile();

        public static bool HasCompletedROM => ROMProfile.IsComplete;

        public static void Reset()
        {
            ROMProfile = new UserROMProfile();
        }
    }
}
