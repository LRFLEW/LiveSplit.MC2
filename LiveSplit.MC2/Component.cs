using System;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.MC2
{
    class Component : LogicComponent
    {
        public override string ComponentName => "Midnight Club 2 Autosplitter";
        
        LiveSplitState _state;
        Hooks _hooks;
        Timer _timer;
        Settings _settings;

        public Component(LiveSplitState state)
        {
            _state = state;
            _hooks = new Hooks(this);
            _settings = new Settings();

            _timer = new Timer { Interval = 15 };
            _timer.Tick += UpdateHook;
            _timer.Enabled = true;
        }

        public override void Dispose()
        {
            _timer.Enabled = false;
            _timer.Dispose();
            _hooks.Dispose();
        }

        private void UpdateHook(object sender, EventArgs e)
        {
            _timer.Enabled = false;
            _hooks.Update();
            _timer.Enabled = true;
        }

        public void On_Loading(bool loading) => _state.IsGameTimePaused = loading;

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }

        public override XmlNode GetSettings(XmlDocument document) => _settings.GetSettings(document);

        public override Control GetSettingsControl(LayoutMode mode) => _settings;

        public override void SetSettings(XmlNode settings) => _settings.SetSettings(settings);
    }
}
