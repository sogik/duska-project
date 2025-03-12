namespace WindowsFormsApp2
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TextBox usuarioBox;
        private System.Windows.Forms.TextBox contrasenaBox;
        private System.Windows.Forms.Button button1; // Registrar usuario
        private System.Windows.Forms.Button button2; // Iniciar sesión
        private System.Windows.Forms.Button button3; // Listar jugadores
        private System.Windows.Forms.Button button4; // Listar partidas
        private System.Windows.Forms.Button button5; // Listar partidas ganadas

        private System.Windows.Forms.Label usuarioLbl; // Etiqueta para usuarioBox
        private System.Windows.Forms.Label contrasenaLbl; // Etiqueta para contrasenaBox

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        private void InitializeComponent()
        {
            this.usuarioBox = new System.Windows.Forms.TextBox();
            this.contrasenaBox = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.usuarioLbl = new System.Windows.Forms.Label();
            this.contrasenaLbl = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // usuarioBox
            // 
            this.usuarioBox.Location = new System.Drawing.Point(16, 33);
            this.usuarioBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.usuarioBox.Name = "usuarioBox";
            this.usuarioBox.Size = new System.Drawing.Size(132, 22);
            this.usuarioBox.TabIndex = 2;
            // 
            // contrasenaBox
            // 
            this.contrasenaBox.Location = new System.Drawing.Point(16, 81);
            this.contrasenaBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.contrasenaBox.Name = "contrasenaBox";
            this.contrasenaBox.PasswordChar = '*';
            this.contrasenaBox.Size = new System.Drawing.Size(132, 22);
            this.contrasenaBox.TabIndex = 3;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(13, 121);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 28);
            this.button1.TabIndex = 5;
            this.button1.Text = "Registrarse";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(121, 121);
            this.button2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 28);
            this.button2.TabIndex = 6;
            this.button2.Text = "Iniciar sesión";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(293, 27);
            this.button3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(164, 28);
            this.button3.TabIndex = 7;
            this.button3.Text = "Listar jugadores";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(293, 75);
            this.button4.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(164, 28);
            this.button4.TabIndex = 8;
            this.button4.Text = "Listar partidas";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(293, 121);
            this.button5.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(164, 29);
            this.button5.TabIndex = 9;
            this.button5.Text = "Listar partidas ganadas";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // usuarioLbl
            // 
            this.usuarioLbl.AutoSize = true;
            this.usuarioLbl.Location = new System.Drawing.Point(16, 14);
            this.usuarioLbl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.usuarioLbl.Name = "usuarioLbl";
            this.usuarioLbl.Size = new System.Drawing.Size(54, 16);
            this.usuarioLbl.TabIndex = 0;
            this.usuarioLbl.Text = "Usuario";
            // 
            // contrasenaLbl
            // 
            this.contrasenaLbl.AutoSize = true;
            this.contrasenaLbl.Location = new System.Drawing.Point(16, 62);
            this.contrasenaLbl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.contrasenaLbl.Name = "contrasenaLbl";
            this.contrasenaLbl.Size = new System.Drawing.Size(76, 16);
            this.contrasenaLbl.TabIndex = 1;
            this.contrasenaLbl.Text = "Contraseña";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1067, 554);
            this.Controls.Add(this.usuarioLbl);
            this.Controls.Add(this.contrasenaLbl);
            this.Controls.Add(this.usuarioBox);
            this.Controls.Add(this.contrasenaBox);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button5);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
