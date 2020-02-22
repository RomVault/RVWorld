using System;
using System.Windows.Forms;
using RVCore;
using RVCore.RvDB;

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

        private Label _labelGameTotalRoms;
        private TextBox _textGameTotalRoms;

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




        private void AddGameMetaData()
        {
            AddTextBox(0, "Name", 6, 84, out _labelGameName, out _textGameName);
            AddTextBox(1, "Description", 6, 84, out _labelGameDescription, out _textGameDescription);
            AddTextBox(2, "Manufacturer", 6, 84, out _labelGameManufacturer, out _textGameManufacturer);

            AddTextBox(3, "Clone of", 6, 84, out _labelGameCloneOf, out _textGameCloneOf);
            AddTextBox(3, "Year", 206, 284, out _labelGameYear, out _textGameYear);

            AddTextBox(4, "Rom of", 6, 84, out _labelGameRomOf, out _textGameRomOf);
            AddTextBox(4, "Total ROMs", 206, 284, out _labelGameTotalRoms, out _textGameTotalRoms);

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
        }


        private void UpdateGameMetaData(RvFile tGame)
        {
            _labelGameName.Visible = true;
            _textGameName.Text = tGame.Name;
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

                _labelGameTotalRoms.Visible = false;
                _textGameTotalRoms.Visible = false;
            }


            if (tGame.Game != null)
            {
                if (tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
                {
                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    _textGameDescription.Text = tGame.Game.GetData(RvGame.GameData.Description);

                    _labelTruripPublisher.Visible = true;
                    _textTruripPublisher.Visible = true;
                    _textTruripPublisher.Text = tGame.Game.GetData(RvGame.GameData.Publisher);

                    _labelTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Visible = true;
                    _textTruripDeveloper.Text = tGame.Game.GetData(RvGame.GameData.Developer);


                    _labelTruripTitleId.Visible = true;
                    _textTruripTitleId.Visible = true;
                    _textTruripTitleId.Text = tGame.Game.GetData(RvGame.GameData.TitleId);

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

                    LoadPannels(tGame);
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
                        HidePannel();

                    _labelGameDescription.Visible = true;
                    _textGameDescription.Visible = true;
                    _textGameDescription.Text = tGame.Game.GetData(RvGame.GameData.Description);

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

                    _labelGameTotalRoms.Visible = true;
                    _textGameTotalRoms.Visible = true;
                }
            }
            else
            {
                HidePannel();
            }
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

            _labelGameTotalRoms.Left = label2Left;
            _textGameTotalRoms.Left = text2Left;
            _textGameTotalRoms.Width = textWidth;


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
