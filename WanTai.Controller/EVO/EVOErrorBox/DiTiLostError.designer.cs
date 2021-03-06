﻿namespace WanTai.Controller.EVO.EVOErrorBox
{
    partial class DiTiLostError
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnContLostDiTiPipet = new System.Windows.Forms.Button();
            this.btnAbort = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.Tip = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Status1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // btnContLostDiTiPipet
            // 
            this.btnContLostDiTiPipet.Location = new System.Drawing.Point(11, 216);
            this.btnContLostDiTiPipet.Name = "btnContLostDiTiPipet";
            this.btnContLostDiTiPipet.Size = new System.Drawing.Size(303, 20);
            this.btnContLostDiTiPipet.TabIndex = 7;
            this.btnContLostDiTiPipet.Text = "Continue, but Pipet nothing with Lost DiTi";
            this.btnContLostDiTiPipet.Click += new System.EventHandler(this.btnContLostDiTiPipet_Click);
            // 
            // btnAbort
            // 
            this.btnAbort.Location = new System.Drawing.Point(11, 243);
            this.btnAbort.Name = "btnAbort";
            this.btnAbort.Size = new System.Drawing.Size(303, 20);
            this.btnAbort.TabIndex = 9;
            this.btnAbort.Text = "Abort";
            this.btnAbort.Click += new System.EventHandler(this.btnAbort_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeColumns = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.BackgroundColor = System.Drawing.SystemColors.Control;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Tip,
            this.Status,
            this.Status1});
            this.dataGridView1.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView1.Location = new System.Drawing.Point(12, 12);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(303, 196);
            this.dataGridView1.TabIndex = 16;
            this.dataGridView1.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.dataGridView1_DataBindingComplete);
            // 
            // Tip
            // 
            this.Tip.DataPropertyName = "tip";
            this.Tip.HeaderText = "通道";
            this.Tip.Name = "Tip";
            this.Tip.ReadOnly = true;
            this.Tip.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Status
            // 
            this.Status.DataPropertyName = "statusChn";
            this.Status.HeaderText = "状态";
            this.Status.Name = "Status";
            this.Status.ReadOnly = true;
            this.Status.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.Status.Width = 200;
            // 
            // Status1
            // 
            this.Status1.DataPropertyName = "status";
            this.Status1.HeaderText = "Status";
            this.Status1.Name = "Status1";
            this.Status1.Visible = false;
            // 
            // DiTiLostError
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(329, 385);
            this.ControlBox = false;
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.btnAbort);
            this.Controls.Add(this.btnContLostDiTiPipet);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "DiTiLostError";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Lost DITI";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnContLostDiTiPipet;
        private System.Windows.Forms.Button btnAbort;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Tip;
        private System.Windows.Forms.DataGridViewTextBoxColumn Status;
        private System.Windows.Forms.DataGridViewTextBoxColumn Status1;

    }
}