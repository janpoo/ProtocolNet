using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Text;
using System.Windows.Forms;

using LumiSoft.Net;
using LumiSoft.Net.FTP;

namespace FTPSVR
{
    public partial class FTPConsole : Form
    {
        public FTPConsole()
        {
            InitializeComponent();
        }

        

   
        private void btnStart_Click(object sender, EventArgs e)
        {            
            
            server.StartServer(this.richTextBox1.Text+"\r\n");
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            log("FTP服务器已启动...(端口:21)");
        }

        void server_AuthUser(object sender, LumiSoft.Net.FTP.Server.AuthUser_EventArgs e)
        {
            e.Validated = false;
            //log("test");
            
            //throw new NotImplementedException();
        }

        private void btnStop_Click_1(object sender, EventArgs e)
        {
            server.StopServer();
            btnStart.Enabled = true;
            btnStop.Enabled = false;

            log( "FTP服务器已关闭...(端口:21)");
        }

        private void FTPConsole_Load(object sender, EventArgs e)
        {
            this.richTextBox1.Text = "220 Serv-U FTP Server v6.4 ready...";
        }

        private void FTPConsole_FormClosing(object sender, FormClosingEventArgs e)
        {
            server.StopServer();

        }

        private void log(string msg)
        {
            this.richTextBox3.Text += System.DateTime.Now.ToString() +"\r\n" + msg +"\r\n";
        }
    }
}
