using System.Reflection;
using LiveSplit.Model;
using LiveSplit.UI.Components;

namespace LiveSplit.MC2
{
    public class ComponentFactory : IComponentFactory
    {
        public string ComponentName => "Midnight Club 2 Autosplitter";
        public string Description => "Load Remover for Midnight Club 2 for PC";
        public ComponentCategory Category => ComponentCategory.Control;
        public IComponent Create(LiveSplitState state) => new Component(state);
        public string UpdateName => this.ComponentName;
        public string UpdateURL => "https://raw.githubusercontent.com/LRFLEW/LiveSplit.MC2/";
        public string XMLURL => this.UpdateURL + "master/Components/Updates.xml";
        public System.Version Version => Assembly.GetExecutingAssembly().GetName().Version;
    }
}
