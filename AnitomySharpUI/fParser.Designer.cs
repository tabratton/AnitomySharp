namespace AnitomySharpUI
{
  partial class fParser
  {
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      txtInput = new TextBox();
      cmdParse = new Button();
      txtOutput = new TextBox();
      SuspendLayout();
      // 
      // txtInput
      // 
      txtInput.Anchor =  AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      txtInput.Location = new Point(12, 12);
      txtInput.Name = "txtInput";
      txtInput.PlaceholderText = "Enter/Paste Anime Filename";
      txtInput.Size = new Size(533, 23);
      txtInput.TabIndex = 0;
      // 
      // cmdParse
      // 
      cmdParse.Anchor =  AnchorStyles.Top | AnchorStyles.Right;
      cmdParse.Location = new Point(551, 12);
      cmdParse.Name = "cmdParse";
      cmdParse.Size = new Size(75, 23);
      cmdParse.TabIndex = 1;
      cmdParse.Text = "Parse";
      cmdParse.UseVisualStyleBackColor = true;
      cmdParse.Click += cmdParse_Click;
      // 
      // txtOutput
      // 
      txtOutput.Anchor =  AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      txtOutput.Location = new Point(12, 41);
      txtOutput.Multiline = true;
      txtOutput.Name = "txtOutput";
      txtOutput.ReadOnly = true;
      txtOutput.ScrollBars = ScrollBars.Vertical;
      txtOutput.Size = new Size(614, 267);
      txtOutput.TabIndex = 2;
      // 
      // fParser
      // 
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(638, 320);
      Controls.Add(txtOutput);
      Controls.Add(cmdParse);
      Controls.Add(txtInput);
      Name = "fParser";
      Text = "Anime Parser";
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private TextBox txtInput;
    private Button cmdParse;
    private TextBox txtOutput;
  }
}