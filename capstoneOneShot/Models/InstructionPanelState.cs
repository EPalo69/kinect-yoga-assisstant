using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace capstoneOneShot.Models
{
    public enum InstructionItemStatus
    {
        Pending,
        Active,
        Completed,
        Regressed
    }

    public class InstructionItem : INotifyPropertyChanged
    {
        private InstructionItemStatus _status;
        private string _text;

        public string JointName { get; set; }

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(nameof(Text)); }
        }

        public InstructionItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(BorderColor));
                OnPropertyChanged(nameof(TextColor));
            }
        }

        // ── Fixed colors per state ────────────────────────────────
        public string StatusIcon
        {
            get
            {
                switch (Status)
                {
                    case InstructionItemStatus.Completed:
                        return "\uE73E"; // ✔ CheckMark

                    case InstructionItemStatus.Active:
                        return "\uEDBB"; // ▶ Active arrow

                    case InstructionItemStatus.Regressed:
                        return "\uE7BA"; // ⚠ Warning

                    default:
                        return "\uE73F"; // ○ Pending circle
                }
            }
        }

        public string StatusColor
        {
            get
            {
                switch (Status)
                {
                    case InstructionItemStatus.Completed:
                        return "#4CAF50";

                    case InstructionItemStatus.Active:
                        return "#64B5F6";

                    case InstructionItemStatus.Regressed:
                        return "#FF6B6B";

                    default:
                        return "#555577";
                }
            }
        }

        public string BackgroundColor
        {
            get
            {
                switch (Status)
                {
                    case InstructionItemStatus.Completed:
                        return "#0D2818";

                    case InstructionItemStatus.Active:
                        return "#1A2744";

                    case InstructionItemStatus.Regressed:
                        return "#2A1010";

                    default:
                        return "#151530";
                }
            }
        }

        public string BorderColor
        {
            get
            {
                switch (Status)
                {
                    case InstructionItemStatus.Completed:
                        return "#4CAF50";

                    case InstructionItemStatus.Active:
                        return "#64B5F6";

                    case InstructionItemStatus.Regressed:
                        return "#FF6B6B";

                    default:
                        return "#2A2A4A";
                }
            }
        }

        public string TextColor
        {
            get
            {
                switch (Status)
                {
                    case InstructionItemStatus.Completed:
                        return "#A5D6A7";

                    case InstructionItemStatus.Active:
                        return "#E3F2FD";

                    case InstructionItemStatus.Regressed:
                        return "#FFCDD2";

                    default:
                        return "#8888AA";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}