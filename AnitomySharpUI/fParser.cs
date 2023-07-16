using AnitomySharp;
using System.Text;

namespace AnitomySharpUI
{
  public partial class fParser : Form
  {
    public fParser()
    {
      InitializeComponent();
    }

    private void cmdParse_Click(object sender, EventArgs e)
    {
      StringBuilder sb = new StringBuilder();
      foreach (Element x in AnitomySharp.AnitomySharp.Parse(txtInput.Text))
      {
        sb.AppendLine(x.Category + ": " + x.Value);
      }
      txtOutput.Text = sb.ToString();
    }
  }
}