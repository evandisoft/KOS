using kOS.Safe.Encapsulation;
using kOS.Safe.Encapsulation.Suffixes;
using kOS.Safe.Execution;
using UnityEngine;


namespace kOS.Suffixed.Widget
{
    [kOS.Safe.Utilities.KOSNomenclature("TextField")]
    public class TextField : Label
    {
        private bool changed;
        public bool Changed
        {
            get { return changed; }
            set
            {
                bool oldVal = changed;
                changed = value;
                if (changed && !oldVal)
                    ScheduleOnChange();
            }
        }

        private bool confirmed;
        public bool Confirmed
        {
            get { return confirmed; }
            set
            {
                bool oldVal = confirmed;
                confirmed = value;
                if (confirmed && !oldVal)
                    ScheduleOnConfirm();
            }
        }

        private Procedure UserOnChange { get; set; }
        private Procedure UserOnConfirm { get; set; }

        private WidgetStyle toolTipStyle;

        /// <summary>
        /// Tracks Unity's ID of this gui widget for the sake of seeing if the widget has focus.
        /// </summary>
        private int uiID = -1;

        /// <summary>
        /// True if this gui widget had the keyboard focus on the previous OnGUI pass:
        /// </summary>
        private bool hadFocus = false;

        public TextField(Box parent, string text) : base(parent,text,parent.FindStyle("textField"))
        {
            toolTipStyle = FindStyle("labelTipOverlay");
            RegisterInitializer(InitializeSuffixes);
        }

        private void InitializeSuffixes()
        {
            AddSuffix("CHANGED", new SetSuffix<BooleanValue>(() => TakeChange(), value => Changed = value));
            AddSuffix("CONFIRMED", new SetSuffix<BooleanValue>(() => TakeConfirm(), value => Confirmed = value));
            AddSuffix("ONCHANGE", new SetSuffix<Procedure>(() => CallbackGetter(UserOnChange), value => UserOnChange = CallbackSetter(value)));
            AddSuffix("ONCONFIRM", new SetSuffix<Procedure>(() => CallbackGetter(UserOnConfirm), value => UserOnConfirm = CallbackSetter(value)));
        }

        public bool TakeChange()
        {
            bool r = Changed;
            Changed = false;
            return r;
        }

        public bool TakeConfirm()
        {
            bool r = Confirmed;
            Confirmed = false;
            return r;
        }

        private void ScheduleOnConfirm()
        {
            if (UserOnConfirm != null)
            {
                if (guiCaused)
                    GetProcessManager().AddToCurrentTriggers(UserOnConfirm);
                else
                    GetProcessManager().InterruptCurrentThread(UserOnConfirm);
                Confirmed = false;
            }
        }

        private void ScheduleOnChange()
        {
            if (UserOnChange != null)
            {
                if (guiCaused)
                    GetProcessManager().AddToCurrentTriggers(UserOnChange);
                else
                    GetProcessManager().InterruptCurrentThread(UserOnChange);
                Changed = false;
            }
        }

        public override void DoGUI()
        {
            bool shouldConfirm = false;
            if (GUIUtility.keyboardControl == uiID)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    shouldConfirm = true;
                hadFocus = true;
            }
            else
            {
                if (hadFocus)
                    shouldConfirm = true;
                hadFocus = false;
            }
            if (shouldConfirm)
            {
                Communicate(() => Confirmed = true);
                GUIUtility.keyboardControl = -1;
            }

            uiID = GUIUtility.GetControlID(FocusType.Passive) + 1; // Dirty kludge.
            string newtext = GUILayout.TextField(VisibleText(), ReadOnlyStyle);
            if (newtext != VisibleText()) {
                SetVisibleText(newtext);
                Changed = true;
            }
            if (newtext == "") {
                GUI.Label(GUILayoutUtility.GetLastRect(), VisibleTooltip(), toolTipStyle.ReadOnly);
            }
        }

        public override string ToString()
        {
            return "TEXTFIELD(" + StoredText().Ellipsis(10) + ")";
        }
    }
}
