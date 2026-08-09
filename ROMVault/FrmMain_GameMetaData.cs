using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using CHDSharpLib;
using RomVaultCore;
using RomVaultCore.RvDB;
using RVIO;
using RVUtils;

namespace ROMVault
{
    public partial class FrmMain
    {
        private Label _labelGameName;
        private TextBox _textGameName;

        private Label _labelGameDescription;
        private TextBox _textGameDescription;

        private Label _labelGameManufacturer;
        private TextBox _textGameManufacturer;

        private Label _labelGameCloneOf;
        private TextBox _textGameCloneOf;

        private Label _labelGameRomOf;
        private TextBox _textGameRomOf;

        private Label _labelGameYear;
        private TextBox _textGameYear;

        private Label _labelGameCategory;
        private TextBox _textGameCategory;

        private Label _labelChdSummary;
        private TextBox _textChdSummary;

        private Label _labelChdLayout;
        private TextBox _textChdLayout;

        private Label _labelChdHashes;
        private TextBox _textChdHashes;

        //Trurip Extra Data
        private Label _labelTruripPublisher;
        private TextBox _textTruripPublisher;

        private Label _labelTruripDeveloper;
        private TextBox _textTruripDeveloper;

        private Label _labelTruripTitleId;
        private TextBox _textTruripTitleId;

        private Label _labelTruripSource;
        private TextBox _textTruripSource;

        private Label _labelTruripCloneOf;
        private TextBox _textTruripCloneOf;

        private Label _labelTruripRelatedTo;
        private TextBox _textTruripRelatedTo;


        private Label _labelTruripYear;
        private TextBox _textTruripYear;

        private Label _labelTruripPlayers;
        private TextBox _textTruripPlayers;


        private Label _labelTruripGenre;
        private TextBox _textTruripGenre;

        private Label _labelTruripSubGenre;
        private TextBox _textTruripSubGenre;


        private Label _labelTruripRatings;
        private TextBox _textTruripRatings;

        private Label _labelTruripScore;
        private TextBox _textTruripScore;



        private void AddTextBox(int line, string name, int x, int x1, out Label lBox, out TextBox tBox)
        {
            int y = 14 + line * 16;

            lBox = new Label
            {
                Location = SPoint(x, y + 1),
                Size = SSize(x1 - x - 2, 13),
                Text = name + @" :",
                TextAlign = ContentAlignment.TopRight
            };
            tBox = new TextBox
            {
                AutoSize = false,
                Location = SPoint(x1, y),
                Size = SSize(20, 17),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                TabStop = false
            };
            gbSetInfo.Controls.Add(lBox);
            gbSetInfo.Controls.Add(tBox);
        }


        private Point SPoint(int x, int y)
        {
            return new Point((int)(x * _scaleFactorX), (int)(y * _scaleFactorY));
        }

        private Size SSize(int x, int y)
        {
            return new Size((int)(x * _scaleFactorX), (int)(y * _scaleFactorY));
        }



        private void AddGameMetaData()
        {
            AddTextBox(0, "Name", 6, 84, out _labelGameName, out _textGameName);
            AddTextBox(1, "Description", 6, 84, out _labelGameDescription, out _textGameDescription);
            AddTextBox(2, "Manufacturer", 6, 84, out _labelGameManufacturer, out _textGameManufacturer);

            AddTextBox(3, "Clone of", 6, 84, out _labelGameCloneOf, out _textGameCloneOf);
            AddTextBox(3, "Year", 206, 284, out _labelGameYear, out _textGameYear);

            AddTextBox(4, "Rom of", 6, 84, out _labelGameRomOf, out _textGameRomOf);
            AddTextBox(4, "Category", 206, 284, out _labelGameCategory, out _textGameCategory);

            AddTextBox(5, "CHD", 6, 84, out _labelChdSummary, out _textChdSummary);
            AddTextBox(6, "Layout", 6, 84, out _labelChdLayout, out _textChdLayout);
            AddTextBox(7, "Hashes", 6, 84, out _labelChdHashes, out _textChdHashes);

            //Trurip

            AddTextBox(2, "Publisher", 6, 84, out _labelTruripPublisher, out _textTruripPublisher);
            AddTextBox(2, "Title Id", 406, 484, out _labelTruripTitleId, out _textTruripTitleId);

            AddTextBox(3, "Developer", 6, 84, out _labelTruripDeveloper, out _textTruripDeveloper);
            AddTextBox(3, "Source", 406, 484, out _labelTruripSource, out _textTruripSource);

            AddTextBox(4, "Clone of", 6, 84, out _labelTruripCloneOf, out _textTruripCloneOf);
            AddTextBox(5, "Related to", 6, 84, out _labelTruripRelatedTo, out _textTruripRelatedTo);

            AddTextBox(6, "Year", 6, 84, out _labelTruripYear, out _textTruripYear);
            AddTextBox(6, "Genre", 206, 284, out _labelTruripGenre, out _textTruripGenre);
            AddTextBox(6, "Ratings", 406, 484, out _labelTruripRatings, out _textTruripRatings);

            AddTextBox(7, "Players", 6, 84, out _labelTruripPlayers, out _textTruripPlayers);
            AddTextBox(7, "SubGenre", 206, 284, out _labelTruripSubGenre, out _textTruripSubGenre);
            AddTextBox(7, "Score", 406, 484, out _labelTruripScore, out _textTruripScore);


            gbSetInfo_Resize(null, new EventArgs());
            UpdateGameMetaData(new RvFile(FileType.Dir));

            //       _textGameName.Click += _textGameName_Click;
            //       _textTruripTitleId.Click += _textTruripTitleId_Click;
        }
        /*
        private void _textGameName_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_textTruripTitleId.Visible)
                    return;
                if (string.IsNullOrWhiteSpace(_textTruripTitleId.Text))
                    return;
                try{Process.Start($"http://database.trurip.org/st/st{_textTruripTitleId.Text}"); } catch { }
            }
            catch (Exception)
            {
            }
        }

        private void _textTruripTitleId_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_textTruripTitleId.Text))
                    return;
                try{Process.Start($"http://database.trurip.org/st/st{_textTruripTitleId.Text}"); } catch { }
            }
            catch (Exception)
            {
            }
        }
        */

        private void UpdateGameMetaData(RvFile tGame)
        {
            gbSetInfo.Text = "Game Info :";
            HideChdMetaData();
            _labelGameName.Visible = true;
            _textGameName.Text = tGame.Name;
            string gameId = tGame.Game?.GetData(RvGame.GameData.Id);
            if (!string.IsNullOrWhiteSpace(gameId))
                _textGameName.Text += $" (ID:{gameId})";

            if (tGame.Game == null)
            {
                _labelGameDescription.Visible = false;
                _textGameDescription.Visible = false;
            }

            if (tGame.Game == null || tGame.Game.GetData(RvGame.GameData.EmuArc) != "yes")
            {
                _labelTruripPublisher.Visible = false;
                _textTruripPublisher.Visible = false;

                _labelTruripDeveloper.Visible = false;
                _textTruripDeveloper.Visible = false;

                _labelTruripTitleId.Visible = false;
                _textTruripTitleId.Visible = false;

                _labelTruripSource.Visible = false;
                _textTruripSource.Visible = false;

                _labelTruripCloneOf.Visible = false;
                _textTruripCloneOf.Visible = false;

                _labelTruripRelatedTo.Visible = false;
                _textTruripRelatedTo.Visible = false;

                _labelTruripYear.Visible = false;
                _textTruripYear.Visible = false;

                _labelTruripPlayers.Visible = false;
                _textTruripPlayers.Visible = false;

                _labelTruripGenre.Visible = false;
                _textTruripGenre.Visible = false;

                _labelTruripSubGenre.Visible = false;
                _textTruripSubGenre.Visible = false;

                _labelTruripRatings.Visible = false;
                _textTruripRatings.Visible = false;

                _labelTruripScore.Visible = false;
                _textTruripScore.Visible = false;
            }

            if (tGame.Game == null || tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
            {
                _labelGameManufacturer.Visible = false;
                _textGameManufacturer.Visible = false;

                _labelGameCloneOf.Visible = false;
                _textGameCloneOf.Visible = false;

                _labelGameRomOf.Visible = false;
                _textGameRomOf.Visible = false;

                _labelGameYear.Visible = false;
                _textGameYear.Visible = false;

                _labelGameCategory.Visible = false;
                _textGameCategory.Visible = false;
            }


            if (tGame.Game != null)
            {
                if (tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
                {
                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    string desc = tGame.Game.GetData(RvGame.GameData.Description);
                    if (desc == "¤") desc = Path.GetFileNameWithoutExtension(tGame.Name);
                    _textGameDescription.Text = desc;

                    _labelTruripPublisher.Visible = true;
                    _textTruripPublisher.Visible = true;
                    _textTruripPublisher.Text = tGame.Game.GetData(RvGame.GameData.Publisher);

                    _labelTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Text = tGame.Game.GetData(RvGame.GameData.Developer);


                    _labelTruripTitleId.Visible = true;
                    _textTruripTitleId.Visible = true;
                    _textTruripTitleId.Text = tGame.Game.GetData(RvGame.GameData.Id);

                    _labelTruripSource.Visible = true;
                    _textTruripSource.Visible = true;
                    _textTruripSource.Text = tGame.Game.GetData(RvGame.GameData.Source);

                    _labelTruripCloneOf.Visible = true;
                    _textTruripCloneOf.Visible = true;
                    _textTruripCloneOf.Text = tGame.Game.GetData(RvGame.GameData.CloneOf);

                    _labelTruripRelatedTo.Visible = true;
                    _textTruripRelatedTo.Visible = true;
                    _textTruripRelatedTo.Text = tGame.Game.GetData(RvGame.GameData.RelatedTo);

                    _labelTruripYear.Visible = true;
                    _textTruripYear.Visible = true;
                    _textTruripYear.Text = tGame.Game.GetData(RvGame.GameData.Year);

                    _labelTruripPlayers.Visible = true;
                    _textTruripPlayers.Visible = true;
                    _textTruripPlayers.Text = tGame.Game.GetData(RvGame.GameData.Players);

                    _labelTruripGenre.Visible = true;
                    _textTruripGenre.Visible = true;
                    _textTruripGenre.Text = tGame.Game.GetData(RvGame.GameData.Genre);

                    _labelTruripSubGenre.Visible = true;
                    _textTruripSubGenre.Visible = true;
                    _textTruripSubGenre.Text = tGame.Game.GetData(RvGame.GameData.SubGenre);

                    _labelTruripRatings.Visible = true;
                    _textTruripRatings.Visible = true;
                    _textTruripRatings.Text = tGame.Game.GetData(RvGame.GameData.Ratings);

                    _labelTruripScore.Visible = true;
                    _textTruripScore.Visible = true;
                    _textTruripScore.Text = tGame.Game.GetData(RvGame.GameData.Score);

                    LoadTruRipPannel(tGame);
                }
                else
                {
                    bool found = false;
                    string path = tGame.Parent.DatTreeFullName;
                    foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
                    {
                        if (path.Length <= 8)
                            continue;

                        if (!string.Equals(path.Substring(8), ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        if (string.IsNullOrWhiteSpace(ei.ExtraPath))
                            continue;

                        if (ei.ExtraPath != null)
                        {
                            found = true;
                            if (ei.ExtraPath.Substring(0, 1) == "%")
                                LoadMameSLPannels(tGame, ei.ExtraPath.Substring(1));
                            else
                                LoadMamePannels(tGame, ei.ExtraPath);

                            break;
                        }
                    }

                    if (!found)
                        found = LoadNFOPannel(tGame);

                    if (!found)
                        found = LoadC64Pannel(tGame);

                    if (!found)
                        HidePannel();

                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    string desc = tGame.Game.GetData(RvGame.GameData.Description);
                    if (desc == "¤") desc = Path.GetFileNameWithoutExtension(tGame.Name);
                    _textGameDescription.Text = desc;

                    _labelGameManufacturer.Visible = true;
                    _textGameManufacturer.Visible = true;
                    _textGameManufacturer.Text = tGame.Game.GetData(RvGame.GameData.Manufacturer);

                    _labelGameCloneOf.Visible = true;
                    _textGameCloneOf.Visible = true;
                    _textGameCloneOf.Text = tGame.Game.GetData(RvGame.GameData.CloneOf);

                    _labelGameRomOf.Visible = true;
                    _textGameRomOf.Visible = true;
                    _textGameRomOf.Text = tGame.Game.GetData(RvGame.GameData.RomOf);

                    _labelGameYear.Visible = true;
                    _textGameYear.Visible = true;
                    _textGameYear.Text = tGame.Game.GetData(RvGame.GameData.Year);

                    _labelGameCategory.Visible = true;
                    _textGameCategory.Visible = true;
                    _textGameCategory.Text = tGame.Game.GetData(RvGame.GameData.Category);
                }
            }
            else
            {
                HidePannel();
            }

            UpdateChdMetaData(tGame);

            this.ActiveControl = GameGrid;
        }


        private void HideChdMetaData()
        {
            _labelChdSummary.Visible = false;
            _textChdSummary.Visible = false;
            _labelChdLayout.Visible = false;
            _textChdLayout.Visible = false;
            _labelChdHashes.Visible = false;
            _textChdHashes.Visible = false;
        }

        private void UpdateChdMetaData(RvFile chd)
        {
            if (chd?.FileType != FileType.CHD || chd.GotStatus == GotStatus.NotGot)
                return;

            bool hasDatGameInfo = chd.Game != null;
            int firstLine = hasDatGameInfo ? 5 : 1;
            SetChdMetaDataLine(_labelChdSummary, _textChdSummary, firstLine);
            SetChdMetaDataLine(_labelChdLayout, _textChdLayout, firstLine + 1);
            SetChdMetaDataLine(_labelChdHashes, _textChdHashes, firstLine + 2);

            // CHD details take the optional final TruRip rows when both kinds of metadata exist.
            if (hasDatGameInfo && chd.Game.GetData(RvGame.GameData.EmuArc) == "yes")
            {
                _labelTruripRelatedTo.Visible = false;
                _textTruripRelatedTo.Visible = false;
                _labelTruripYear.Visible = false;
                _textTruripYear.Visible = false;
                _labelTruripPlayers.Visible = false;
                _textTruripPlayers.Visible = false;
                _labelTruripGenre.Visible = false;
                _textTruripGenre.Visible = false;
                _labelTruripSubGenre.Visible = false;
                _textTruripSubGenre.Visible = false;
                _labelTruripRatings.Visible = false;
                _textTruripRatings.Visible = false;
                _labelTruripScore.Visible = false;
                _textTruripScore.Visible = false;
            }

            _labelChdSummary.Visible = true;
            _textChdSummary.Visible = true;
            gbSetInfo.Text = hasDatGameInfo ? "Game / CHD Info :" : "CHD Info :";

            string path = chd.FullNameCase;
            if (!ChdMetadata.TryReadContainerInfo(path, out ChdContainerInfo info, out string error))
            {
                _textChdSummary.Text = "Unable to read CHD header: " + error;
                tooltip.SetToolTip(_textChdSummary, _textChdSummary.Text);
                return;
            }

            string profileMetadata = "";
            ChdMetadata.TryReadTextMetadata(path, "RVEP", 0, out profileMetadata, out _);
            _textChdSummary.Text = FormatChdSummary(info, profileMetadata);
            _textChdLayout.Text = FormatChdLayout(info);
            _textChdHashes.Text = FormatChdHashes(chd, info);
            _labelChdLayout.Visible = true;
            _textChdLayout.Visible = true;
            _labelChdHashes.Visible = true;
            _textChdHashes.Visible = true;

            string details = _textChdSummary.Text + Environment.NewLine +
                             _textChdLayout.Text + Environment.NewLine +
                             _textChdHashes.Text;
            if (!string.IsNullOrWhiteSpace(chd.ChdScanMethod))
                details += Environment.NewLine + "Scan: " + chd.ChdScanMethod;
            if (!string.IsNullOrWhiteSpace(chd.ChdHashMatchMode))
                details += Environment.NewLine + "Hash match: " + chd.ChdHashMatchMode;
            if (!string.IsNullOrWhiteSpace(chd.ChdDescriptorMatch))
                details += Environment.NewLine + "Descriptor: " + chd.ChdDescriptorMatch;
            if (!string.IsNullOrWhiteSpace(chd.ChdStatus))
                details += Environment.NewLine + "Status: " + chd.ChdStatus;
            tooltip.SetToolTip(_textChdSummary, details);
            tooltip.SetToolTip(_textChdLayout, details);
            tooltip.SetToolTip(_textChdHashes, details);
        }

        private void SetChdMetaDataLine(Label label, TextBox textBox, int line)
        {
            label.Top = SPoint(0, 15 + line * 16).Y;
            textBox.Top = SPoint(0, 14 + line * 16).Y;
        }

        internal static string FormatChdSummary(ChdContainerInfo info, string profileMetadata)
        {
            if (info == null)
                return "";

            string profile = DescribeChdProfile(profileMetadata);
            string codecs = info.Codecs == null || info.Codecs.Count == 0 ? "none" : string.Join(", ", info.Codecs);
            return "V" + info.Version +
                   (string.IsNullOrWhiteSpace(profile) ? "" : " | " + profile) +
                   " | Codecs " + codecs;
        }

        internal static string FormatChdLayout(ChdContainerInfo info)
        {
            if (info == null)
                return "";

            return "Logical " + info.LogicalSize.ToString("N0") + " bytes" +
                   " | Hunk " + info.HunkSize.ToString("N0") +
                   " | Unit " + info.UnitSize.ToString("N0") +
                   " | " + (info.RequiresParent ? "Parent required" : "Standalone");
        }

        internal static string FormatChdHashes(RvFile chd, ChdContainerInfo info)
        {
            List<string> hashes = new List<string>();
            AddHash(hashes, "File CRC32", chd?.CRC);
            AddHash(hashes, "File SHA1", chd?.SHA1);
            AddHash(hashes, "File MD5", chd?.MD5);
            AddHash(hashes, "CHD SHA1", info?.Sha1);
            AddHash(hashes, "Raw SHA1", info?.RawSha1);
            if (info?.RequiresParent == true)
                AddHash(hashes, "Parent SHA1", info.ParentSha1);
            return hashes.Count == 0 ? "Unavailable" : string.Join(" | ", hashes);
        }

        private static void AddHash(List<string> hashes, string name, byte[] value)
        {
            string hex = value.ToHexString();
            if (!string.IsNullOrWhiteSpace(hex))
                hashes.Add(name + " " + hex);
        }

        internal static string DescribeChdProfile(string metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
                return "";

            string profile = "";
            string storage = "";
            string family = "";
            string[] values = metadata.Split(';');
            for (int i = 0; i < values.Length; i++)
            {
                int separator = values[i].IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = values[i].Substring(0, separator).Trim();
                string value = values[i].Substring(separator + 1).Trim();
                if (string.Equals(key, "profile", StringComparison.OrdinalIgnoreCase)) profile = value;
                else if (string.Equals(key, "storage", StringComparison.OrdinalIgnoreCase)) storage = value;
                else if (string.Equals(key, "family", StringComparison.OrdinalIgnoreCase)) family = value;
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(profile)) parts.Add(profile);
            if (!string.IsNullOrWhiteSpace(storage)) parts.Add(storage);
            if (!string.IsNullOrWhiteSpace(family)) parts.Add(family);
            return string.Join(" / ", parts);
        }

        private void gbSetInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 84;
            int rightPos = gbSetInfo.Width - 15;
            if (rightPos > 750)
            {
                rightPos = 750;
            }

            int width = rightPos - leftPos;


            if (_textGameName == null)
            {
                return;
            }

            // main Meta Data
            int textWidth = (int)((double)width * 120 / 340);
            int text2Left = leftPos + width - textWidth;
            int label2Left = text2Left - 78;

            _textGameName.Width = width;
            _textGameDescription.Width = width;
            _textGameManufacturer.Width = width;

            _textGameCloneOf.Width = textWidth;

            _labelGameYear.Left = label2Left;
            _textGameYear.Left = text2Left;
            _textGameYear.Width = textWidth;

            _textGameRomOf.Width = textWidth;

            _labelGameCategory.Left = label2Left;
            _textGameCategory.Left = text2Left;
            _textGameCategory.Width = textWidth;

            _textChdSummary.Width = width;
            _textChdLayout.Width = width;
            _textChdHashes.Width = width;


            // TruRip Meta Data
            textWidth = (int)(width * 0.20);
            text2Left = (int)(width * 0.4 + leftPos);
            label2Left = text2Left - 78;
            int text3Left = leftPos + width - textWidth;
            int label3Left = text3Left - 78;

            _textTruripPublisher.Width = (int)(width * 0.6);
            _textTruripDeveloper.Width = (int)(width * 0.6);
            _textTruripCloneOf.Width = width;
            _textTruripRelatedTo.Width = width;

            _textTruripYear.Width = textWidth;
            _textTruripPlayers.Width = textWidth;

            _labelTruripGenre.Left = label2Left;
            _textTruripGenre.Left = text2Left;
            _textTruripGenre.Width = textWidth;

            _labelTruripSubGenre.Left = label2Left;
            _textTruripSubGenre.Left = text2Left;
            _textTruripSubGenre.Width = textWidth;


            _labelTruripTitleId.Left = label3Left;
            _textTruripTitleId.Left = text3Left;
            _textTruripTitleId.Width = textWidth;

            _labelTruripSource.Left = label3Left;
            _textTruripSource.Left = text3Left;
            _textTruripSource.Width = textWidth;

            _labelTruripRatings.Left = label3Left;
            _textTruripRatings.Left = text3Left;
            _textTruripRatings.Width = textWidth;

            _labelTruripScore.Left = label3Left;
            _textTruripScore.Left = text3Left;
            _textTruripScore.Width = textWidth;
        }
    }
}
