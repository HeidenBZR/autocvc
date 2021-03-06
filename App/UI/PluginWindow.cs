﻿using System;
using System.Windows.Forms;
using VAtlas;
using LyricInputHelper.UI;
using System.Drawing;

namespace LyricInputHelper
{
    public partial class PluginWindow : Form
    {
        public static PluginWindow window;
        static SetTextWindow SetTextWindow;
        public AtlasSettings Settings = new AtlasSettings();
        public Version VERSION = new Version(0, 4, 2, 0);

        public PluginWindow()
        {
            window = this;
            InitializeComponent();
        }

        void SetTitle()
        {
            Text = $"LyricInputHelper v.{VERSION.ToString()} ({Singer.Current.Name} - {Atlas.VoicebankType})";
        }

        void ChangeLang()
        {
            Lang.Set((string)comboBoxLanguage.SelectedItem);
            SetLang();
            SetTitle();
            if (SetTextWindow != null)
                SetTextWindow.SetLang();
        }

        void SetLang()
        {
            checkBoxInsertShort.Text = Lang.Get("checkbox_insert_short");
            checkBoxVR.Text = Lang.Get("checkbox_insert_vr");
            
            labelOfMinLength.Text = Lang.Get("min_length");
            labelOfMultiplayer.Text = Lang.Get("multiplayer");
            labelOfCompressionRatio.Text = Lang.Get("compression_ratio");
            labelOfLastChildCompressionRatio.Text = Lang.Get("last_child_compression_ratio");

            buttonOk.Text = Lang.Get("button_ok");
            buttonCancel.Text = Lang.Get("button_cancel");

            tabPageMain.Text = Lang.Get("pluginwindow_page_main");
            tabPageOptions.Text = Lang.Get("pluginwindow_page_options");

            checkBoxLengthByOto.Text = Lang.Get("oto_by_length");
            checkBoxFade.Text = Lang.Get("fade");

        }


        public void GetValues()
        {
            if (double.TryParse(textBoxCompressionRatio.Text, out double ratio))
                if (ratio > 0)
                    Settings.CompressionRatio = ratio;
            if (double.TryParse(textBoxCompressionRatio.Text, out double lchratio))
                if (ratio > 0)
                    Settings.LastChildCompressionRatio = lchratio;
            if (int.TryParse(textBoxMinLength.Text, out int minLength))
                Settings.MinLength = minLength;
            if (double.TryParse(textBoxVelocity.Text, out double velocity))
                Settings.Velocity = velocity;
            Settings.MakeVR = checkBoxVR.Checked;
            Settings.MakeShort = checkBoxInsertShort.Checked;
            Settings.LengthByOto = checkBoxLengthByOto.Checked;
            Settings.MakeFade = checkBoxFade.Checked;
        }

        public void Recolor(DataGridView lyricView)
        {
            Color SelectionForeColor = Color.DarkGray;
            Color LightColor = Color.FromArgb(220, 230, 240);
            Color MediumColor = Color.FromArgb(180, 190, 190);
            Color DarkColor = Color.FromArgb(80, 110, 120);
            Color GreenColor = Color.FromArgb(196, 239, 214);
            Color InsertColor = Color.FromArgb(230, 232, 237);
            DataGridViewCellStyle defaultCellStyle = lyricView.DefaultCellStyle.Clone();
            defaultCellStyle.SelectionBackColor = Color.LightBlue;
            defaultCellStyle.SelectionForeColor = DarkColor;

            DataGridViewCellStyle darkCellStyle = defaultCellStyle.Clone();
            darkCellStyle.BackColor = DarkColor;
            darkCellStyle.SelectionBackColor = DarkColor;
            darkCellStyle.SelectionForeColor = MediumColor;
            darkCellStyle.ForeColor = MediumColor;

            DataGridViewCellStyle insertCellStyle = defaultCellStyle.Clone();
            insertCellStyle.BackColor = InsertColor;
            insertCellStyle.SelectionBackColor = Color.LightSkyBlue;
            insertCellStyle.SelectionForeColor = DarkColor;

            DataGridViewCellStyle deleteCellStyle = defaultCellStyle.Clone();
            deleteCellStyle.ForeColor = Color.DarkGray;
            deleteCellStyle.BackColor = Color.LightGray;
            deleteCellStyle.SelectionBackColor = Color.DimGray;
            deleteCellStyle.SelectionForeColor = Color.LightGray;

            DataGridViewCellStyle lyricCellStyle = defaultCellStyle.Clone();
            lyricCellStyle.BackColor = GreenColor;

            lyricView.Columns[0].DefaultCellStyle = darkCellStyle;
            lyricView.DefaultCellStyle = defaultCellStyle;

            for (int y = 0; y < lyricView.Rows.Count; y++)
            {
                Note note = Ust.Notes[y];
                if (note.Number == NumberManager.INSERT)
                {
                    for (int x = 1; x < 3; x++)
                        lyricView[x, y].Style = insertCellStyle;
                }
                if (note.IsRest)
                {
                    for (int x = 0; x < lyricView.Columns.Count; x++)
                        lyricView[x, y].Style = darkCellStyle;
                }
                if (note.Number == NumberManager.DELETE)
                {
                    for (int x = 1; x < lyricView.Columns.Count; x++)
                        lyricView[x, y].Style = deleteCellStyle;
                }
                //if (note.Syllable != null)
                    lyricView[3, y].Style = darkCellStyle;
                //if (note.WordName != null)
                    lyricView[4, y].Style = darkCellStyle;

            }
        }

        public void SetStatus(string text, bool appendTextbox = false)
        {
            if (window != null)
                window.labelStatus.Text = text;
        }

        public void CheckAccess()
        {
            window.buttonSetText.Enabled = !Settings.IsUnparsed;
            window.buttonSplit.Enabled = !Settings.IsUnparsed;
            window.buttonAtlasConvert.Enabled = !Settings.IsUnparsed;
            window.buttonToCV.Enabled = !Settings.IsParsed;
            window.buttonToCVC.Enabled = !Settings.IsParsed;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            GetValues();
            Ust.Save(Settings);
            Application.Exit();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void PluginWindow_Load(object sender, EventArgs e)
        {
            Recolor(lyricView);
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            Reset();
        }

        private void buttonSetText_Click(object sender, EventArgs e)
        {
            SetText();
        }

        private void buttonSplit_Click(object sender, EventArgs e)
        {
            Split();
        }

        private void buttonAtlasConvert_Click(object sender, EventArgs e)
        {
            Convert();
        }

        private void buttonToCVC_Click(object sender, EventArgs e)
        {
            ToCV();
        }

        private void buttonToCV_Click(object sender, EventArgs e)
        {
            ToCV();
        }

        private void buttonReloadResources_Click(object sender, EventArgs e)
        {
            Reload();
            MessageBox.Show("Вокалист и атлас были перезагружены. Словарь загружается", "Обновление ресурсов");
        }

        private void buttonAddWord_Click(object sender, EventArgs e)
        {
            AddWord();
        }

        private void buttonSetText_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_set_text");
        }

        private void buttonSplit_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_split");
        }

        private void buttonAtlasConvert_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_atlas_convert");
        }

        private void buttonReset_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_reset");
        }

        private void buttonToCVC_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_to_cv_c");
        }

        private void buttonToCV_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_to_cv");
        }

        private void buttonAddWord_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_add_word");
        }

        private void buttonReloadResources_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("button_reload_resources");
        }

        private void textBoxCompressionRatio_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("compression_ratio_tooltip");
        }

        private void buttonFade_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("fade_tooltip");
        }

        private void textBoxLastChildCompressionRatio_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("last_child_compression_ratio_tooltip");
        }

        private void checkBoxLengthByOto_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("oto_by_length_tooltip");
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            ChangeLang();
        }

        private void checkBoxFade_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("fade_tooltip");
        }

        private void checkBoxVR_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("make_vr_tooltip");
        }

        private void textBoxMinLength_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("min_length_tooltip");
        }

        private void comboBoxLanguage_MouseEnter(object sender, EventArgs e)
        {
            labelStatus.Text = Lang.Get("language_tooltip");
        }

        private void lyricView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var i = e.RowIndex;
                ((DataGridView)sender)[2, i].ToolTipText = Ust.Notes[i].AliasType;
        }


        private void PluginWindow_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void lyricView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 2)
            {
                EditNote(e.RowIndex);
            }

        }
    }
}
