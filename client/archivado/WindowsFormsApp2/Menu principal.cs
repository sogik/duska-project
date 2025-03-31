using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;

namespace WindowsFormsApp2
{
    public partial class Form1 : Form
    {
        Socket server;
        Thread atender;
        delegate void DelegadoParaEscribirTexto(string text);

        public Form1()
        {
            InitializeComponent();
            this.BackColor = Color.Gray;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("192.168.56.102");
                IPEndPoint ipep = new IPEndPoint(direc, 9050);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    server.Connect(ipep);

                    if (server.Connected)
                    {
                        this.BackColor = Color.Green;
                        MessageBox.Show("Conectado con exito");
                    }
                    // Registrar usuario
                    string mensaje = "0/" + usuarioBox.Text + "/" + contrasenaBox.Text;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    byte[] msg2 = new byte[80];
                    server.Receive(msg2);
                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    MessageBox.Show("El resultado es: " + mensaje);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("No he podido conectar con el servidor: " + ex.Message);
                    return;
                }

                this.BackColor = Color.Gray;
                server.Shutdown(SocketShutdown.Both);
                server.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("192.168.56.102");
                IPEndPoint ipep = new IPEndPoint(direc, 9050);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    server.Connect(ipep);

                    if (server.Connected)
                    {
                        this.BackColor = Color.Green;
                        MessageBox.Show("Conectado con exito");
                    }
                        // Iniciar sesión
                    string mensaje = "1/" + usuarioBox.Text + "/" + contrasenaBox.Text;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    byte[] msg2 = new byte[80];
                    server.Receive(msg2);
                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    MessageBox.Show("El resultado es: " + mensaje);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("No he podido conectar con el servidor: " + ex.Message);
                    return;
                }

                this.BackColor = Color.Gray;
                server.Shutdown(SocketShutdown.Both);
                server.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("192.168.56.102");
                IPEndPoint ipep = new IPEndPoint(direc, 9050);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    server.Connect(ipep);

                    if (server.Connected)
                    {
                        this.BackColor = Color.Green;
                        MessageBox.Show("Conectado con exito");
                    }
                    // Listar jugadores
                    string mensaje = "2/" + usuarioBox.Text + "/" + contrasenaBox.Text;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    byte[] msg2 = new byte[80];
                    server.Receive(msg2);
                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    MessageBox.Show("El resultado es: " + mensaje);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("No he podido conectar con el servidor: " + ex.Message);
                    return;
                }

                this.BackColor = Color.Gray;
                server.Shutdown(SocketShutdown.Both);
                server.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("192.168.56.102");
                IPEndPoint ipep = new IPEndPoint(direc, 9050);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    server.Connect(ipep);

                    if (server.Connected)
                    {
                        this.BackColor = Color.Green;
                        MessageBox.Show("Conectado con exito");
                    }
                    // Listar partidas
                    string mensaje = "3/" + usuarioBox.Text + "/" + contrasenaBox.Text;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    byte[] msg2 = new byte[80];
                    server.Receive(msg2);
                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    MessageBox.Show("El resultado es: " + mensaje);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("No he podido conectar con el servidor: " + ex.Message);
                    return;
                }

                this.BackColor = Color.Gray;
                server.Shutdown(SocketShutdown.Both);
                server.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("192.168.56.102");
                IPEndPoint ipep = new IPEndPoint(direc, 9050);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    server.Connect(ipep);

                    if (server.Connected)
                    {
                        this.BackColor = Color.Green;
                        MessageBox.Show("Conectado con exito");
                    }
                    // Listar partidas ganadas
                    string mensaje = "4/" + usuarioBox.Text + "/" + contrasenaBox.Text;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    byte[] msg2 = new byte[80];
                    server.Receive(msg2);
                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    MessageBox.Show("El resultado es: " + mensaje);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("No he podido conectar con el servidor: " + ex.Message);
                    return;
                }

                this.BackColor = Color.Gray;
                server.Shutdown(SocketShutdown.Both);
                server.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}
