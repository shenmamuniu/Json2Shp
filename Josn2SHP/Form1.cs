using LM.GIS;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Josn2SHP
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            string jsonPath = @"C:\Users\hasee\Desktop\1000.json";
            FeatureLyrHelper helper2 = new FeatureLyrHelper();
            helper2.ResolveJson(jsonPath);

            sw.Stop();
            string elapsedTime=sw.Elapsed.ToString();
            Debug.WriteLine(elapsedTime);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
