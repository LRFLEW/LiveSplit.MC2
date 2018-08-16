using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.MC2
{
    public partial class Settings : UserControl
    {
        public Settings()
        {
            InitializeComponent();
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            XmlElement settingsNode = document.CreateElement("Settings");

            return settingsNode;
        }

        public void SetSettings(XmlNode settings)
        {
            XmlElement element = (XmlElement) settings;
        }
    }
}
