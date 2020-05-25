﻿using SearchControls.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
#if NET45 || NET451 || NET452
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using SearchControls.Export;
using System.Reflection;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace SearchControls
{
    /// <summary>
    /// 按钮群的方法
    /// </summary>
    [
        DisplayName("ButtonFlowLayoutPanelMethod"),
        Description("按钮群的方法接口"),
        DefaultEvent("FoundCompleted"),
        ToolboxItem(true)
    ]
    public partial class ButtonFlowLayoutPanelMethod : Component , IButtonFlowLayoutPanelMethod
    {
        private ButtonFlowLayoutPanel buttonFlowLayoutPanel;
        ButtonFlowLayoutPanel IButtonFlowLayoutPanelMethod.ButtonFlowLayoutPanel 
        { 
            get => buttonFlowLayoutPanel;
            set
            {
                buttonFlowLayoutPanel = value;
                if (dataGridView != null)
                {
                    SetButtonFlowLayoutPanel();
                }
            }
        }
        /// <summary>
        /// 文件标题
        /// </summary>
        [
            Description("文件标题")
        ]
        public string FileTitle { get; set; }
        /// <summary>
        /// 文件路径
        /// </summary>
        [
            Description("文件路径")
        ]
        public string FilePath { get; set; }
        /// <summary>
        /// 需要转换为首字母拼音的列名
        /// </summary>
        [
            Description("需要转换为首字母拼音的列名")
        ]
        public string[] InitialsDataColumnNames { get; set; } = new string[0];
        /// <summary>
        /// 向上向下的方式
        /// </summary>
        [
            Description("向上向下的方式")
        ]
        public enum UpDownMethod 
        { 
            /// <summary>
            /// 循环
            /// </summary>
            Loop,
            /// <summary>
            /// 普通
            /// </summary>
            None 
        }
        /// <summary>
        /// 向上的方式
        /// </summary>
        [
            Description("向上的方式")
        ]
        public UpDownMethod UpMethod { get; set; } = UpDownMethod.Loop;
        /// <summary>
        /// 向下的方式
        /// </summary>
        [
            Description("向下的方式")
        ]
        public UpDownMethod DownMethod { get; set; } = UpDownMethod.Loop;
        private SqlDataAdapter selectSqlDataAdapter;
        /// <summary>
        /// <para>查询用的<see cref="SqlDataAdapter"/></para>
        /// <para>MissingSchemaAction默认为<see cref="MissingSchemaAction"/>.AddWithKey(抓取主键)</para>
        /// <para>FillLoadOption默认为<see cref="LoadOption"/>.OverwriteChanges</para>
        /// </summary>
        [
            Description("查询用的SqlDataAdapter")
        ]
        public SqlDataAdapter SelectSqlDataAdapter
        {
            get => selectSqlDataAdapter;
            set
            {
                selectSqlDataAdapter = value;
                if (selectSqlDataAdapter != null)
                {
                    selectSqlDataAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                    selectSqlDataAdapter.FillLoadOption = LoadOption.OverwriteChanges;
                }
            }
        }
        private SqlDataAdapter updateSqlDataAdapter;
        /// <summary>
        /// 更新用的<see cref="SqlDataAdapter"/>
        /// </summary>
        /// <remarks>默认与SelectSqlDataAdapter一致，当没有添加、删除、更新的语句时，会自动根据查询语句自动生成</remarks>
        [
            Description("更新用的SqlDataAdapter")
        ]
        public SqlDataAdapter UpdateSqlDataAdapter
        {
            get => updateSqlDataAdapter ?? selectSqlDataAdapter;
            set => updateSqlDataAdapter = value;
        }
        /// <summary>
        /// 封装窗体的数据源<see cref="System.Windows.Forms.BindingSource"/>
        /// </summary>
        [
            Description("封装窗体的数据源BindingSource")
        ]
        public BindingSource BindingSource { get; } = new BindingSource();
        private string tableName;
        /// <summary>
        /// 表名
        /// </summary>
        [
            Description("表名")
        ]
        public string TableName
        {
            get => tableName;
            set
            {
                if (DataSet.Tables.Contains(value)) BindingSource.DataMember = value;
                tableName = value;
            }
        }
        /// <summary>
        /// 表示数据在内存中的缓存。
        /// </summary>
        [
            Description("表示数据在内存中的缓存。")
        ]
        public DataSet DataSet { get; } = new DataSet();
        private DataGridView dataGridView;
        /// <summary>
        /// 需要控制的对应网格
        /// </summary>
        [
            Description("需要控制的对应网格")
        ]
        public DataGridView DataGridView
        {
            get => dataGridView;
            set
            {
                if (value != null)
                {
                    dataGridView = value;
                    dataGridView.ParentChanged += (sender, e) =>
                      {
                          if (dataGridView.TopLevelControl is Form f)
                          {
                              EventHandlerList eventHandlerList = (EventHandlerList)typeof(Form).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f, null);
                              FieldInfo fieldInfo = typeof(Form).GetField("EVENT_FORMCLOSING", BindingFlags.Static | BindingFlags.NonPublic);

                              Delegate d = eventHandlerList[fieldInfo.GetValue(null)];
                              if (d == null)
                              {
                                  f.FormClosing += (f_sender, f_e) =>
                                  {
                                      if (buttonFlowLayoutPanel.BtnUpdate.Enabled && !DataGridView.Rows.Cast<DataGridViewRow>().Count(dgvr => !dgvr.IsNewRow).Equals(0) && DataSet.GetChanges() != null)
                                      {
                                          DialogResult dr = MessageBox.Show("是否将更改提交到 数据库 中？", f.Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                                          switch (dr)
                                          {
                                              case DialogResult.Yes:
                                                  try
                                                  {
                                                      BtnUpdate_Click();
                                                  }
                                                  catch
                                                  {
                                                      f_e.Cancel = true;
                                                  }
                                                  break;
                                              case DialogResult.Cancel:
                                                  f_e.Cancel = true;
                                                  break;
                                          }
                                      }
                                  };
                              }
                          }
                      };

                    if (buttonFlowLayoutPanel != null)
                    {
                        SetButtonFlowLayoutPanel();
                    }
                }
            }
        }

        private bool isSetButtonFlowLayoutPanel = false;
        private void SetButtonFlowLayoutPanel()
        {
            if (isSetButtonFlowLayoutPanel) return;
            isSetButtonFlowLayoutPanel = true;

            //dataGridView.KeyDown += Shortcutkey_KeyDown;
            if (dataGridView.TopLevelControl == null)
            {
                dataGridView.HandleCreated += (sender, e) =>
                  {
                      if (sender is DataGridView dgv && dgv.TopLevelControl != null) dgv.TopLevelControl.KeyDown += Shortcutkey_KeyDown;
                  };
            }
            else dataGridView.TopLevelControl.KeyDown += Shortcutkey_KeyDown;
        }

        /// <summary>
        /// 快捷键设定
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">包含事件数据的 System.Windows.Forms.KeyEventArgs。</param>
        public virtual void Shortcutkey_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is Control)
            {
                switch (e.KeyData)
                {
                    case Keys.Insert | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnInsert.Enabled) BtnInsert_Click();
                        break;
                    case Keys.Delete | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnDelete.Enabled) BtnDelete_Click();
                        break;
                    case Keys.Home:
                        if (buttonFlowLayoutPanel.BtnFirst.Enabled) BtnFirst_Click();
                        break;
                    case Keys.End:
                        if (buttonFlowLayoutPanel.BtnLast.Enabled) BtnLast_Click();
                        break;
                    //case Keys.PageUp:
                    //    e.Handled = true;
                    //    if (buttonFlowLayoutPanel.BtnUp.Enabled) BtnUp_Click();
                    //    break;
                    //case Keys.PageDown:
                    //    e.Handled = true;
                    //    if (buttonFlowLayoutPanel.BtnDown.Enabled) BtnDown_Click();
                    //    break;
                    case Keys.F | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnFound.Enabled) BtnFound_Click();
                        break;
                    case Keys.W | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnWord.Enabled) BtnWord_Click();
                        break;
                    case Keys.E | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnExcel.Enabled) BtnExcel_Click();
                        break;
                    case Keys.R | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnCancel.Enabled) BtnCancel_Click();
                        break;
                    case Keys.S | Keys.Control:
                        if (buttonFlowLayoutPanel.BtnUpdate.Enabled) BtnUpdate_Click();
                        break;
                        //case Keys.Escape:
                        //    if (buttonFlowLayoutPanel.BtnQuit.Enabled) BtnQuit_Click();
                        //    break;
                }
            }
        }

        /// <summary>
        /// Export[0]:转换用的字段,
        /// Export[1]:对应的中文字段
        /// </summary>
        [
            Description("Export[0]:转换用的字段,\r\nExport[1]:对应的中文字段")
        ]
        public string[][] ExcelExportColumns { get; } = new string[2][];
        /// <summary>
        /// Export[0]:转换用的字段,
        /// Export[1]:对应的中文字段
        /// </summary>
        [
            Description("Export[0]:转换用的字段,\r\nExport[1]:对应的中文字段")
        ]
        public string[][] WordExportColumns { get; } = new string[2][];

        /// <summary>
        /// <para>当第一按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当第一按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnFirstClick;
        /// <summary>
        /// <para>当向下按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当向下按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnDownClick;
        /// <summary>
        /// <para>当向上按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当向上按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnUpClick;
        /// <summary>
        /// <para>当最后按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当最后按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnLastClick;
        /// <summary>
        /// <para>当添加按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当添加按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnInsertClick;
        /// <summary>
        /// <para>当删除按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当删除按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnDeleteClick;
        /// <summary>
        /// <para>当提交按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当提交按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnUpdateClick;
        /// <summary>
        /// 当更新中出现错误时
        /// </summary>
        [
            Description("当更新中出现错误时")
        ]
        public event ErrorEventHandler ErrorUpdate;
        /// <summary>
        /// <para>当查找按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当查找按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnFoundClick;
        /// <summary>
        /// 当查找完成时
        /// </summary>
        [
            Description("当查找完成时")
        ]
        public event EventHandler FoundCompleted;
        /// <summary>
        /// 当查找中出现错误时
        /// </summary>
        [
            Description("当查找中出现错误时")
        ]
        public event ErrorEventHandler ErrorFound;
        /// <summary>
        /// <para>当Excel按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当Excel按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnExcelClick;
        /// <summary>
        /// <para>当Word按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当Word按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnWordClick;
        /// <summary>
        /// <para>当撤销按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当撤销按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnCancelClick;
        /// <summary>
        /// <para>当退出按钮点击时</para>
        /// <para>当e.Handle = true时为重写事件</para>
        /// <para>添加事件时e.Handle默认为true</para>
        /// </summary>
        [
            Description("当退出按钮点击时\r\n当e.Handle = true时为重写事件\r\n添加事件时e.Handle默认为true")
        ]
        public event HandledEventHandler BtnQuitClick;
        /// <summary>
        /// 当Excel初始化完成后发生
        /// </summary>
        [
            Description("当Excel初始化完成后发生")
        ]
        public event ExcelInitiatedEventHandler ExcelInitiated;
        /// <summary>
        /// 当DataTable转换Excel完成后发生(不包括保存成文件)
        /// </summary>
        [
            Description("当DataTable转换Excel完成后发生(不包括保存成文件)")
        ]
        public event ExcelCompletedEventHandler ExcelCompleted;
        /// <summary>
        /// 当Word初始化完成后发生
        /// </summary>
        [
            Description("当Word初始化完成后发生")
        ]
        public event WordInitiatedEventHandler WordInitiated;
        /// <summary>
        /// 当DataTable转换Word完成后发生(不包括保存成文件)
        /// </summary>
        [
            Description("当DataTable转换Word完成后发生(不包括保存成文件)")
        ]
        public event WordCompletedEventHandler WordCompleted;
        /// <summary>
        /// 第一按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnFirstClick(HandledEventArgs e)
        {
            if (BtnFirstClick != null)
            {
                e.Handled = true;
                BtnFirstClick(this, e);
            }
        }
        /// <summary>
        /// 向下按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnDownClick(HandledEventArgs e)
        {
            if (BtnDownClick != null)
            {
                e.Handled = true;
                BtnDownClick(this, e);
            }
        }
        /// <summary>
        /// 向上按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnUpClick(HandledEventArgs e)
        {
            if (BtnUpClick != null)
            {
                e.Handled = true;
                BtnUpClick(this, e);
            }
        }
        /// <summary>
        /// 最后按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnLastClick(HandledEventArgs e)
        {
            if (BtnLastClick != null)
            {
                e.Handled = true;
                BtnLastClick(this, e);
            }
        }
        /// <summary>
        /// 添加按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnInsertClick(HandledEventArgs e)
        {
            if (BtnInsertClick != null)
            {
                e.Handled = true;
                BtnInsertClick(this, e);
            }
        }
        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnDeleteClick(HandledEventArgs e)
        {
            if (BtnDeleteClick != null)
            {
                e.Handled = true;
                BtnDeleteClick(this, e);
            }
        }
        /// <summary>
        /// 提交按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnUpdateClick(HandledEventArgs e)
        {
            if (BtnUpdateClick != null)
            {
                e.Handled = true;
                BtnUpdateClick(this, e);
            }
        }
        /// <summary>
        /// 查找按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnErrorUpdate(ErrorEventArgs e)
        {
            if (ErrorUpdate != null)
            {
                ErrorUpdate(this, e);
            }
            else
            {
                GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;
                if (gridStatusStrip != null)
                {
                    gridStatusStrip.ToolUpdateStatus.Text = e.GetException().Message;
                    gridStatusStrip.ToolUpdateStatus.ForeColor = Color.Red;
                }
                else MessageBox.Show(e.GetException().Message);
            }
        }
        /// <summary>
        /// 查找按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnFoundClick(HandledEventArgs e)
        {
            if (BtnFoundClick != null)
            {
                e.Handled = true;
                BtnFoundClick(this, e);
            }
        }
        /// <summary>
        /// 查找完成时的事件
        /// </summary>
        /// <param name="e">表示包含事件数据的类的基类，并提供用于不包含事件数据的事件的值。</param>
        protected virtual void OnFoundCompleted(EventArgs e)
        {
            FoundCompleted?.Invoke(this, e);
        }
        /// <summary>
        /// 当查找中出现错误时
        /// </summary>
        /// <param name="e">为 System.IO.FileSystemWatcher.Error 事件提供数据。</param>
        protected virtual void OnErrorFound(ErrorEventArgs e)
        {
            if (dataGridView.InvokeRequired)
            {
                Action<ErrorEventArgs> action = new Action<ErrorEventArgs>(OnErrorFound);
                dataGridView.Invoke(action, e);
            }
            else
            {
                if (ErrorFound != null)
                {
                    ErrorFound(this, e);
                }
                else
                {
                    GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;
                    if (gridStatusStrip != null)
                    {
                        gridStatusStrip.ToolUpdateStatus.Text = e.GetException().Message;
                        gridStatusStrip.ToolUpdateStatus.ForeColor = Color.Red;
                    }
                    else MessageBox.Show(e.GetException().Message);
                }
            }
        }
        /// <summary>
        /// Excel按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnExcelClick(HandledEventArgs e)
        {
            if (BtnExcelClick != null)
            {
                e.Handled = true;
                BtnExcelClick(this, e);
            }
        }
        /// <summary>
        /// Word按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnWordClick(HandledEventArgs e)
        {
            if (BtnWordClick != null)
            {
                e.Handled = true;
                BtnWordClick(this, e);
            }
        }
        /// <summary>
        /// 撤销按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnCancelClick(HandledEventArgs e)
        {
            if (BtnCancelClick != null)
            {
                e.Handled = true;
                BtnCancelClick(this, e);
            }
        }
        /// <summary>
        /// 退出按钮点击事件
        /// </summary>
        /// <param name="e">当e.Handle = true时为重写事件，添加事件时e.Handle默认为true。</param>
        protected virtual void OnBtnQuitClick(HandledEventArgs e)
        {
            if (BtnQuitClick != null)
            {
                e.Handled = true;
                BtnQuitClick(this, e);
            }
        }
        /// <summary>
        /// Excel初始化完成事件
        /// </summary>
        /// <param name="excel"><see cref="Excel"/></param>
        /// <param name="e"><see cref="EventArgs"/></param>
        protected virtual void OnExcelInitiated(Excel excel, EventArgs e) => ExcelInitiated?.Invoke(excel, e);
        /// <summary>
        /// DataTable转换Excel完成事件
        /// </summary>
        /// <param name="excel"><see cref="Excel"/></param>
        /// <param name="e"><see cref="EventArgs"/></param>
        protected virtual void OnExcelCompleted(Excel excel, EventArgs e) => ExcelCompleted?.Invoke(excel, e);
        /// <summary>
        /// Word初始化完成事件
        /// </summary>
        /// <param name="word"><see cref="Word"/></param>
        /// <param name="e"><see cref="EventArgs"/></param>
        protected virtual void OnWordInitiated(Word word, EventArgs e) => WordInitiated?.Invoke(word, e);
        /// <summary>
        /// DataTable转换Word完成事件
        /// </summary>
        /// <param name="word"><see cref="Word"/></param>
        /// <param name="e"><see cref="EventArgs"/></param>
        protected virtual void OnWordCompleted(Word word, EventArgs e) => WordCompleted?.Invoke(word, e);
        /// <summary>
        /// 初始化
        /// </summary>
        public ButtonFlowLayoutPanelMethod()
        {
            InitializeComponent();
            BindingSource.DataSource = DataSet;
            DataSet.Tables.CollectionChanged += (sender, e) =>
              {
                  if (e.Action.Equals(CollectionChangeAction.Add) && e.Element is DataTable dt && dt.TableName.Equals(TableName))
                  {
                      Action action = null;
                      action = () =>
                      {
                          if (buttonFlowLayoutPanel.InvokeRequired) buttonFlowLayoutPanel.Invoke(action);
                          else if (!BindingSource.DataMember.Equals(TableName)) BindingSource.DataMember = TableName;
                      };
                      action();
                  }
              };
        }
        /// <summary>
        /// 点击第一按钮
        /// </summary>
        public void BtnFirst_Click()
        {
            #region "第一"按钮btnFirst单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnFirstClick(he);
            if (he.Handled) return;

            First();

            #endregion
        }
        /// <summary>
        /// 移动到第一行
        /// </summary>
        public virtual void First() => BindingSource.MoveFirst();
        /// <summary>
        /// 点击向下按钮
        /// </summary>
        public void BtnDown_Click()
        {
            #region  "向下"按钮btnDown单击事件(dataGridView1)

            HandledEventArgs he = new HandledEventArgs();
            OnBtnDownClick(he);
            if (he.Handled) return;

            Down();

            #endregion
        }
        /// <summary>
        /// 向下移动一行
        /// </summary>
        public virtual void Down()
        {
            if (DownMethod.Equals(UpDownMethod.None))
            {
                BindingSource.MoveNext();
            }
            else
            {
                if (BindingSource.Position.Equals(BindingSource.Count - 1)) BindingSource.MoveFirst();
                else BindingSource.MoveNext();
            }
        }
        /// <summary>
        /// 点击向上按钮
        /// </summary>
        public void BtnUp_Click()
        {
            #region "向上"按钮btnUp单击事件(dataGridView1)

            HandledEventArgs he = new HandledEventArgs();
            OnBtnUpClick(he);
            if (he.Handled) return;

            Up();

            #endregion
        }
        /// <summary>
        /// 向上移动一行
        /// </summary>
        public void Up()
        {
            if (UpMethod.Equals(UpDownMethod.None))
            {
                BindingSource.MovePrevious();
            }
            else
            {
                if (BindingSource.Position.Equals(0)) BindingSource.MoveLast();
                else BindingSource.MovePrevious();
            }
        }
        /// <summary>
        /// 点击最后按钮
        /// </summary>
        public void BtnLast_Click()
        {
            #region "最后"按钮btnLast单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnLastClick(he);
            if (he.Handled) return;

            Last();

            #endregion
        }
        /// <summary>
        /// 移动到最后一行
        /// </summary>
        public virtual void Last() => BindingSource.MoveLast();
        /// <summary>
        /// 点击添加按钮
        /// </summary>
        public void BtnInsert_Click()
        {
            #region “添加”按钮btnInsert的单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnInsertClick(he);
            if (he.Handled) return;

            Insert();

            #endregion
        }
        /// <summary>
        /// 添加一行
        /// </summary>
        public virtual void Insert()
        {
            #region “添加”按钮方法

            DataRowView drv = BindingSource.AddNew() as DataRowView;
            if (!string.IsNullOrEmpty(BindingSource.Filter))
                BindingSource.Filter += "OR " + DataSet.Tables[TableName].PrimaryKey[0].ColumnName + " = " + drv[DataSet.Tables[TableName].PrimaryKey[0].ColumnName];
            BindingSource.Position = BindingSource.Count - 1;

            #endregion
        }
        /// <summary>
        /// 点击删除按钮
        /// </summary>
        public void BtnDelete_Click()
        {
            #region "删除"按钮btnDelete单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnDeleteClick(he);
            if (he.Handled) return;

            Delete();

            #endregion
        }
        /// <summary>
        /// 删除当前的一行
        /// </summary>
        public virtual void Delete() => BindingSource.RemoveCurrent();
        /// <summary>
        /// 点击提交按钮
        /// </summary>
        public void BtnUpdate_Click()
        {
            #region "提交"按钮btnUpdate单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnUpdateClick(he);
            if (he.Handled) return;

            GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;

            buttonFlowLayoutPanel.BtnUpdate.Enabled = false;

            int val = SqlUpdate();
            if (!val.Equals(0))
            {
                if (gridStatusStrip != null)
                {
                    gridStatusStrip.ToolUpdateStatus.Text = string.Format("更新成功，共计更新{0}条记录", val);
                    gridStatusStrip.ToolUpdateStatus.ForeColor = Color.Black;
                }
                else
                {
                    MessageBox.Show(string.Format("更新成功，共计更新{0}条记录", val));
                }
            }

            buttonFlowLayoutPanel.BtnUpdate.Enabled = true;

            #endregion
        }
        /// <summary>
        /// 将更改的数据提交到数据库
        /// </summary>
        /// <returns>提交成功的行数</returns>
        public virtual int SqlUpdate()
        {
            #region "提交"按钮btnUpdate单击事件

            BindingSource.EndEdit();
            if (DataSet.Tables[TableName].GetChanges() == null)
            {
                OnErrorUpdate(new ErrorEventArgs(new Exception("没有更改任何记录，请更改后再按“提交”按钮")));
                return 0;
            }
            else
            {
                try
                {
                    UpdateSqlDataAdapter.InsertCommand = UpdateSqlDataAdapter.InsertCommand ?? new SqlCommandBuilder(UpdateSqlDataAdapter).GetInsertCommand();
                    UpdateSqlDataAdapter.DeleteCommand = UpdateSqlDataAdapter.DeleteCommand ?? new SqlCommandBuilder(UpdateSqlDataAdapter).GetDeleteCommand();
                    UpdateSqlDataAdapter.UpdateCommand = UpdateSqlDataAdapter.UpdateCommand ?? new SqlCommandBuilder(UpdateSqlDataAdapter).GetUpdateCommand();

                    int val = UpdateSqlDataAdapter.Update(DataSet, TableName);

                    Found();

                    DataSet.Tables[TableName].AcceptChanges();

                    return val;
                }
                catch (Exception ex)
                {
                    OnErrorUpdate(new ErrorEventArgs(new Exception(string.Format("更新失败:{0}", ex.Message), ex)));
                    return 0;
                }
            }

            #endregion
        }

        bool isFounding = false;
        /// <summary>
        /// 点击查找按钮
        /// </summary>
        public async void BtnFound_Click()
        {
            #region "查找"按钮btnFound单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnFoundClick(he);

            buttonFlowLayoutPanel.Controls.Cast<Control>().Where(c => c is Button).ToList().ForEach(b =>
              {
                  switch(b.Name)
                  {
                      case nameof(buttonFlowLayoutPanel.BtnQuit):
                          break;
                      //case nameof(buttonFlowLayoutPanel.BtnFound):
                      //    if (b.Text.Equals("取消"))
                      //        isFounding = true;
                      //    else
                      //        b.Text = "取消";
                      //    break;
                      default:
                          b.Enabled = false;
                          break;
                  }
              });

            GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;
            if (gridStatusStrip != null && !gridStatusStrip.IsDisposed)
            {
                gridStatusStrip.ToolStripProgressBar.Style = ProgressBarStyle.Marquee;
                gridStatusStrip.ToolProgressBarStatus.Text = "正在查询";
            }

            if (!he.Handled) await FoundAsync();

            if(gridStatusStrip != null && !gridStatusStrip.IsDisposed)
            {
                gridStatusStrip.ToolStripProgressBar.Style = ProgressBarStyle.Continuous;
                gridStatusStrip.ToolProgressBarStatus.Text = "查询完成";
            }

            buttonFlowLayoutPanel.Controls.Cast<Control>().Where(c => c is Button).ToList().ForEach(b =>
            {
                switch (b.Name)
                {
                    case nameof(buttonFlowLayoutPanel.BtnQuit):
                        break;
                    //case nameof(buttonFlowLayoutPanel.BtnFound):
                    //    b.Text = "查找";
                    //    isFounding = false;
                    //    break;
                    case nameof(buttonFlowLayoutPanel.BtnUpdate):
                        if (UpdateSqlDataAdapter != null) b.Enabled = true;
                        break;
                    default:
                        b.Enabled = true;
                        break;
                }
            });
            DataSet.Tables[TableName]?.AcceptChanges();

            OnFoundCompleted(new EventArgs());

            #endregion
        }
        /// <summary>
        /// 从数据库中查询最新的数据
        /// </summary>
        public virtual void Found()
        {
            #region "查找"按钮btnFound单击事件

            if (isFounding)
            {
                selectSqlDataAdapter.SelectCommand?.Cancel();
            }
            else
            {
                try
                {
                    DataSet.Tables[tableName]?.Clear();
                    selectSqlDataAdapter.Fill(DataSet, TableName);

                    if (DataSet.Tables[TableName].Columns.Cast<DataColumn>().All(dc => !dc.ColumnName.Contains("PY_")))
                    {
                        Method.CreateManyInitialsDataColumn(DataSet.Tables[TableName], InitialsDataColumnNames);
                    }

                    Action action = null;
                    action = () =>
                      {
                          if (dataGridView.InvokeRequired) dataGridView.Invoke(action);
                          else if (dataGridView.DataSource == null) dataGridView.DataSource = BindingSource;
                      };
                    action();
                }
                catch (SqlException ex)
                {
                    OnErrorFound(new ErrorEventArgs(ex));
                }
                catch (Exception ex)
                {
                    OnErrorFound(new ErrorEventArgs(ex));
                }
            }

            #endregion
        }
        /// <summary>
        /// <see cref="Found()"/>的线程
        /// </summary>
        /// <returns>线程</returns>
        public Task FoundAsync() => Task.Factory.StartNew(Found, TaskCreationOptions.PreferFairness);
        /// <summary>
        /// 点击Excel按钮
        /// </summary>
        public async void BtnExcel_Click()
        {
            #region 导出Excel按钮单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnExcelClick(he);
            if (he.Handled) return;

            GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;

            if (buttonFlowLayoutPanel.BtnExcel.Text.Equals("取消"))
            {
                buttonFlowLayoutPanel.BtnExcel.Text = "EXCEL";
                if (gridStatusStrip != null) gridStatusStrip.ToolProgressBarStatus.Text = "已经取消转换";
                return;
            }

            if (string.IsNullOrEmpty(FilePath))
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = "请选择文件夹"
                };
                if (dialog.ShowDialog().Equals(DialogResult.OK))
                {
                    FilePath = dialog.SelectedPath;
                }
                else return;
            }

            buttonFlowLayoutPanel.BtnExcel.Text = "取消";

            if (gridStatusStrip != null)
            {
                gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Black;
                if (gridStatusStrip.ToolProgressBarStatus.Text.Contains("正在转换"))
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "正在转换Word和Excel";
                    gridStatusStrip.ToolStripProgressBar.Maximum += BindingSource.Count;
                }
                else
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "正在转换Excel";
                    gridStatusStrip.ToolStripProgressBar.Value = 0;
                    gridStatusStrip.ToolStripProgressBar.Maximum = BindingSource.Count;
                }
            }

            try
            {
                int rowindex = 0;
                Export.Progress<ExcelProgressEventArgs> progress = new Export.Progress<ExcelProgressEventArgs>(epe =>
                {
                    if (!rowindex.Equals(epe.RowIndex))
                    {
                        rowindex = epe.RowIndex;
                        if (gridStatusStrip != null && !gridStatusStrip.IsDisposed) gridStatusStrip.Invoke(new Action(() => gridStatusStrip.ToolStripProgressBar.PerformStep()));
                    }
                    if (buttonFlowLayoutPanel.BtnExcel.Text.Equals("EXCEL"))
                    {
                        epe.CancellationTokenSource.Cancel();
                    }
                });

                await ExcelAsync(progress);

                if (!buttonFlowLayoutPanel.IsDisposed)
                {
                    if (gridStatusStrip != null)
                    {
                        gridStatusStrip.ToolProgressBarStatus.Text = "转换Excel成功";
                        gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Black;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (gridStatusStrip != null && !gridStatusStrip.IsDisposed)
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "已取消转换Excel";
                    gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                if (gridStatusStrip != null && !gridStatusStrip.IsDisposed)
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = $"转换失败:{ex.Message}";
                    gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Red;
                }
            }
            finally
            {
                buttonFlowLayoutPanel.BtnExcel.Text = "EXCEL";
            }

            #endregion
        }
        /// <summary>
        /// DataTable转换成Excel
        /// </summary>
        /// <param name="progress">进度条<see cref="ExcelProgressEventArgs"/></param>
        /// <returns>线程</returns>
        public virtual Task ExcelAsync(IProgress<ExcelProgressEventArgs> progress) => Task.Run(async () =>
        {
            string title = FileTitle ?? buttonFlowLayoutPanel.TopLevelControl.Text;

            string fileExtension = ".xlsx"; //文件后缀名
            string path = FilePath.Trim()[FilePath.Trim().Count() - 1].Equals('\\') ? FilePath + title.Replace(" ", "") + fileExtension : FilePath + "\\" + title.Replace(" ", "") + fileExtension;

            if (!Directory.Exists(FilePath)) //如果文件夹不存在则创建一个文件夹
            {
                Directory.CreateDirectory(FilePath);
            }

            using (Excel excel = new Excel(BindingSource.List as DataView, ExcelExportColumns[0], ExcelExportColumns[1], title))
            {
                excel.ResetDefault();
                OnExcelInitiated(excel, new EventArgs());

                await excel.DataTableToExcelAsync(progress);

                OnExcelCompleted(excel, new EventArgs());
                excel.SaveAs(path, true);
            }
        });
        /// <summary>
        /// 点击Word按钮
        /// </summary>
        public async void BtnWord_Click()
        {
            #region 导出Word按钮单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnWordClick(he);
            if (he.Handled) return;

            GridStatusStrip gridStatusStrip = buttonFlowLayoutPanel.TopLevelControl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(fieldInfo => fieldInfo.FieldType != null && fieldInfo.FieldType.Equals(typeof(GridStatusStrip)))?.GetValue(buttonFlowLayoutPanel.TopLevelControl) as GridStatusStrip;

            if (buttonFlowLayoutPanel.BtnWord.Text.Equals("取消"))
            {
                buttonFlowLayoutPanel.BtnWord.Text = "WORD";
                if (gridStatusStrip != null) gridStatusStrip.ToolProgressBarStatus.Text = "已经取消转换";
                return;
            }

            if (string.IsNullOrEmpty(FilePath))
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = "请选择文件夹"
                };
                if (dialog.ShowDialog().Equals(DialogResult.OK))
                {
                    FilePath = dialog.SelectedPath;
                }
                else return;
            }

            buttonFlowLayoutPanel.BtnWord.Text = "取消";

            if (gridStatusStrip != null)
            {
                gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Black;
                if (gridStatusStrip.ToolProgressBarStatus.Text.Contains("正在转换"))
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "正在转换Excel和Word";
                    gridStatusStrip.ToolStripProgressBar.Maximum += BindingSource.Count;
                }
                else
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "正在转换Word";
                    gridStatusStrip.ToolStripProgressBar.Value = 0;
                    gridStatusStrip.ToolStripProgressBar.Maximum = BindingSource.Count;
                }
            }

            try
            {
                int rowindex = 0;
                Export.Progress<WordProgressEventArgs> progress = new Export.Progress<WordProgressEventArgs>(epe =>
                {
                    if (!rowindex.Equals(epe.RowIndex))
                    {
                        rowindex = epe.RowIndex;
                        if (gridStatusStrip != null && !gridStatusStrip.IsDisposed) gridStatusStrip.Invoke(new Action(() => gridStatusStrip.ToolStripProgressBar.PerformStep()));
                    }
                    if (buttonFlowLayoutPanel.BtnWord.Text.Equals("WORD"))
                    {
                        epe.CancellationTokenSource.Cancel();
                    }
                });

                await WordAsync(progress);

                if (!buttonFlowLayoutPanel.IsDisposed)
                {
                    if (gridStatusStrip != null)
                    {
                        gridStatusStrip.ToolProgressBarStatus.Text = "转换Word成功";
                        gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Black;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (gridStatusStrip != null && !gridStatusStrip.IsDisposed)
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = "已取消转换Word";
                    gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                if (gridStatusStrip != null && !gridStatusStrip.IsDisposed)
                {
                    gridStatusStrip.ToolProgressBarStatus.Text = $"转换失败:{ex.Message}";
                    gridStatusStrip.ToolProgressBarStatus.ForeColor = Color.Red;
                }
            }
            finally
            {
                buttonFlowLayoutPanel.BtnWord.Text = "WORD";
            }

            #endregion
        }
        /// <summary>
        /// DataTable转换成Word
        /// </summary>
        /// <param name="progress">进度条<see cref="WordProgressEventArgs"/></param>
        /// <returns>线程</returns>
        public virtual Task WordAsync(IProgress<WordProgressEventArgs> progress) => Task.Run(async () =>
        {
            string title = FileTitle ?? buttonFlowLayoutPanel.TopLevelControl.Text;

            string fileExtension = ".docx"; //文件后缀名
            string path = FilePath.Trim()[FilePath.Trim().Count() - 1].Equals('\\') ? FilePath + title.Replace(" ", "") + fileExtension : FilePath + "\\" + title.Replace(" ", "") + fileExtension;

            if (!Directory.Exists(FilePath)) //如果文件夹不存在则创建一个文件夹
            {
                Directory.CreateDirectory(FilePath);
            }

            using (Word word = new Word(BindingSource.List as DataView, WordExportColumns[0], WordExportColumns[1], title))
            {
                if (Regex.IsMatch(title, @"[\u4e00-\u9fa5]")) word.TitleRun.FontFamily = "宋体";
                word.ResetDefault();

                OnWordInitiated(word, new EventArgs());

                await word.DataTableToWordAsync(progress);

                OnWordCompleted(word, new EventArgs());
                word.SaveAs(path, true);
            }
        });
        /// <summary>
        /// 点击撤销按钮
        /// </summary>
        public void BtnCancel_Click()
        {
            #region "撤销"按钮btnCancel单击事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnCancelClick(he);
            if (he.Handled) return;

            Cancel();

            #endregion
        }
        /// <summary>
        /// 内存表中更改的内容全部撤销
        /// </summary>
        public virtual void Cancel()
        {
            BindingSource.MoveFirst();
            DataSet.Tables[TableName].RejectChanges();
        }
        /// <summary>
        /// 点击退出按钮
        /// </summary>
        public void BtnQuit_Click()
        {
            #region 退出按钮事件

            HandledEventArgs he = new HandledEventArgs();
            OnBtnQuitClick(he);
            if (he.Handled) return;

            Quit();

            #endregion
        }
        /// <summary>
        /// 退出当前窗口
        /// </summary>
        public virtual void Quit() => (buttonFlowLayoutPanel.TopLevelControl as Form)?.Close();
    }
}
