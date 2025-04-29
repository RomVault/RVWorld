using System;
using System.IO;
using RomVaultCore.RvDB;

namespace ROMVault
{
    public partial class FrmMain
    {
        private void TabArtworkInitialize()
        {
            splitListArt.Panel2Collapsed = true;
            splitListArt.Panel2.Hide();

            tabArtWork_Resize(null, new EventArgs());
            tabMedium_Resize(null, new EventArgs());
            tabScreens_Resize(null, new EventArgs());
            tabInfo_Resize(null, new EventArgs());
        }


        private void tabArtWork_Resize(object sender, EventArgs e)
        {
            int imageWidth = tabArtWork.Width - 20;
            if (imageWidth < 2)
                imageWidth = 2;

            picArtwork.Left = 10;
            picArtwork.Width = imageWidth;
            picArtwork.Top = (int)(tabArtWork.Height * 0.05);
            picArtwork.Height = (int)(tabArtWork.Height * 0.4);

            picLogo.Left = 10;
            picLogo.Width = imageWidth;
            picLogo.Top = (int)(tabArtWork.Height * 0.55);
            picLogo.Height = (int)(tabArtWork.Height * 0.4);
        }

        private void tabMedium_Resize(object sender, EventArgs e)
        {
            int imageWidth = tabMedium.Width - 20;
            if (imageWidth < 2)
                imageWidth = 2;

            picMedium1.Left = 10;
            picMedium1.Width = imageWidth;
            picMedium1.Top = (int)(tabMedium.Height * 0.05);
            picMedium1.Height = (int)(tabMedium.Height * 0.4);

            picMedium2.Left = 10;
            picMedium2.Width = imageWidth;
            picMedium2.Top = (int)(tabMedium.Height * 0.55);
            picMedium2.Height = (int)(tabMedium.Height * 0.4);

        }

        private void tabScreens_Resize(object sender, EventArgs e)
        {
            int imageWidth = tabScreens.Width - 20;
            if (imageWidth < 2)
                imageWidth = 2;

            picScreenTitle.Left = 10;
            picScreenTitle.Width = imageWidth;
            picScreenTitle.Top = (int)(tabScreens.Height * 0.05);
            picScreenTitle.Height = (int)(tabScreens.Height * 0.4);

            picScreenShot.Left = 10;
            picScreenShot.Width = imageWidth;
            picScreenShot.Top = (int)(tabScreens.Height * 0.55);
            picScreenShot.Height = (int)(tabScreens.Height * 0.4);
        }
        private void tabInfo_Resize(object sender, EventArgs e)
        {

        }

        private void LoadMamePannels(RvFile tGame, string extraPath)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);

            string[] path = extraPath.Split('\\');

            RvFile fExtra = DB.DirRoot.Child(0);

            foreach (string p in path)
            {
                if (fExtra.ChildNameSearch(FileType.Dir, p, out int pIndex) != 0)
                    return;
                fExtra = fExtra.Child(pIndex);
            }

            bool artLoaded = false;
            bool logoLoaded = false;

            bool titleLoaded = false;
            bool screenLoaded = false;

            bool storyLoaded = false;

            int index;

            if (fExtra.ChildNameSearch(FileType.Zip, "artpreview.zip", out index) == 0)
            {
                artLoaded = picArtwork.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }
            else if (fExtra.ChildNameSearch(FileType.Dir, "artpreviewsnap", out index) == 0)
            {
                artLoaded = picArtwork.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }

            if (fExtra.ChildNameSearch(FileType.Zip, "marquees.zip", out index) == 0)
            {
                logoLoaded = picLogo.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }
            else if (fExtra.ChildNameSearch(FileType.Dir, "marquees", out index) == 0)
            {
                logoLoaded = picLogo.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }


            if (fExtra.ChildNameSearch(FileType.Zip, "snap.zip", out index) == 0)
            {
                screenLoaded = picScreenShot.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }
            else if (fExtra.ChildNameSearch(FileType.Dir, "snap", out index) == 0)
            {
                screenLoaded = picScreenShot.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }

            if (fExtra.ChildNameSearch(FileType.Zip, "cabinets.zip", out index) == 0)
            {
                titleLoaded = picScreenTitle.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }
            else if (fExtra.ChildNameSearch(FileType.Dir, "cabinets", out index) == 0)
            {
                titleLoaded = picScreenTitle.TryLoadImage(fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
            }


            if (artLoaded || logoLoaded) TabEmuArc.TabPages.Add(tabArtWork);
            if (titleLoaded || screenLoaded) TabEmuArc.TabPages.Add(tabScreens);
            if (storyLoaded) TabEmuArc.TabPages.Add(tabInfo);

            if (artLoaded || logoLoaded || titleLoaded || screenLoaded || storyLoaded)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
            }

        }

        private void LoadMameSLPannels(RvFile tGame, string extraPath)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);
            TabEmuArc.TabPages.Remove(tabInfo2);

            string[] path = extraPath.Split('\\');

            RvFile fExtra = DB.DirRoot.Child(0);

            foreach (string p in path)
            {
                if (fExtra.ChildNameSearch(FileType.Dir, p, out int pIndex) != 0)
                    return;
                fExtra = fExtra.Child(pIndex);
            }

            bool artLoaded = false;
            bool logoLoaded = false;

            bool titleLoaded = false;
            bool screenLoaded = false;

            bool storyLoaded = false;

            int index;



            string fname = tGame.Parent.Name + "/" + Path.GetFileNameWithoutExtension(tGame.Name);

            if (fExtra.ChildNameSearch(FileType.Zip, "covers_SL.zip", out index) == 0)
            {
                artLoaded = picArtwork.TryLoadImage(fExtra.Child(index), fname);
            }

            if (fExtra.ChildNameSearch(FileType.Zip, "snap_SL.zip", out index) == 0)
            {
                logoLoaded = picLogo.TryLoadImage(fExtra.Child(index), fname);
            }

            if (fExtra.ChildNameSearch(FileType.Zip, "titles_SL.zip", out index) == 0)
            {
                screenLoaded = picScreenShot.TryLoadImage(fExtra.Child(index), fname);
            }

            if (artLoaded || logoLoaded) TabEmuArc.TabPages.Add(tabArtWork);
            if (titleLoaded || screenLoaded) TabEmuArc.TabPages.Add(tabScreens);
            if (storyLoaded) TabEmuArc.TabPages.Add(tabInfo);

            if (artLoaded || logoLoaded || titleLoaded || screenLoaded || storyLoaded)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
            }

        }

        // need to only load new image if the RvFile has changed
        // to stop flickering on screen while system is processing
        private void LoadPannelFromRom(RvFile tRom)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);
            TabEmuArc.TabPages.Remove(tabInfo2);

            string ext = Path.GetExtension(tRom.Name).ToLower();
            if (ext != ".png" && ext != ".jpg")
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
                return;
            }
            bool loaded = picArtwork.LoadImage(tRom.Parent, tRom.Name);
            if (loaded)
            {
                TabEmuArc.TabPages.Add(tabArtWork);
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
            }
        }

        private bool LoadC64Pannel(RvFile tGame)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);
            TabEmuArc.TabPages.Remove(tabInfo2);

            bool artLoaded = picArtwork.TryLoadImage(tGame, "Front");
            bool logoLoaded = picLogo.TryLoadImage(tGame, "Extras/Cassette");


            bool titleLoaded = picScreenTitle.TryLoadImage(tGame, "Extras/Inlay");
            bool screenLoaded = picScreenShot.TryLoadImage(tGame, "Extras/Inlay_back");


            if (artLoaded || logoLoaded) TabEmuArc.TabPages.Add(tabArtWork);
            if (titleLoaded || screenLoaded) TabEmuArc.TabPages.Add(tabScreens);

            if (artLoaded || logoLoaded || titleLoaded || screenLoaded)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
                return true;
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
                return false;
            }

        }

        private bool LoadNFOPannel(RvFile tGame)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);
            TabEmuArc.TabPages.Remove(tabInfo2);

            bool storyLoaded = txtInfo.LoadNFO(tGame, "*.nfo");
            if (storyLoaded)
            {
                tabInfo.Text = "NFO";
                TabEmuArc.TabPages.Add(tabInfo);
            }

            bool storyLoaded2 = txtInfo2.LoadNFO(tGame, "*.diz");
            if (storyLoaded2)
            {
                tabInfo2.Text = "DIZ";
                TabEmuArc.TabPages.Add(tabInfo2);
            }
            if (storyLoaded || storyLoaded2)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
                return true;
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
                return false;
            }

        }


        private void LoadTruRipPannel(RvFile tGame)
        {
            TabEmuArc.TabPages.Remove(tabArtWork);
            TabEmuArc.TabPages.Remove(tabMedium);
            TabEmuArc.TabPages.Remove(tabScreens);
            TabEmuArc.TabPages.Remove(tabInfo);
            TabEmuArc.TabPages.Remove(tabInfo2);

            /*
             * artwork_front.png
             * artowrk_back.png
             * logo.png
             * medium_front.png
             * screentitle.png
             * screenshot.png
             * story.txt
             *
             * System.Diagnostics.Process.Start(@"D:\stage\RomVault\RomRoot\SNK\Neo Geo CD (World) - SuperDAT\Games\Double Dragon (19950603)\video.mp4");
             *
             */

            bool artLoaded = picArtwork.TryLoadImage(tGame, "Artwork/artwork_front");
            bool logoLoaded = picLogo.TryLoadImage(tGame, "Artwork/logo");
            if (!logoLoaded)
                logoLoaded = picArtwork.TryLoadImage(tGame, "Artwork/artwork_back");

            bool medium1Loaded = picMedium1.TryLoadImage(tGame, "Artwork/medium_front*");
            bool medium2Loaded = picMedium2.TryLoadImage(tGame, "Artwork/medium_back*");
            bool titleLoaded = picScreenTitle.TryLoadImage(tGame, "Artwork/screentitle");
            bool screenLoaded = picScreenShot.TryLoadImage(tGame, "Artwork/screenshot");
            bool storyLoaded = txtInfo.LoadText(tGame, "Artwork/story.txt");
            if (storyLoaded)
                tabInfo.Text = "Story.txt";

            if (!storyLoaded)
            {
                storyLoaded = txtInfo.LoadNFO(tGame, "*.nfo");
                if (storyLoaded)
                    tabInfo.Text = "NFO";
            }


            if (artLoaded || logoLoaded) TabEmuArc.TabPages.Add(tabArtWork);
            if (medium1Loaded || medium2Loaded) TabEmuArc.TabPages.Add(tabMedium);
            if (titleLoaded || screenLoaded) TabEmuArc.TabPages.Add(tabScreens);
            if (storyLoaded) TabEmuArc.TabPages.Add(tabInfo);

            if (artLoaded || logoLoaded || titleLoaded || screenLoaded || storyLoaded || medium1Loaded || medium2Loaded)
            {
                splitListArt.Panel2Collapsed = false;
                splitListArt.Panel2.Show();
            }
            else
            {
                splitListArt.Panel2Collapsed = true;
                splitListArt.Panel2.Hide();
            }

        }



        private void HidePannel()
        {
            splitListArt.Panel2Collapsed = true;
            splitListArt.Panel2.Hide();

            picArtwork.ClearImage();
            picLogo.ClearImage();
            picMedium1.ClearImage();
            picMedium2.ClearImage();
            picScreenTitle.ClearImage();
            picScreenShot.ClearImage();
            txtInfo.ClearText();
            txtInfo2.ClearText();
        }
    }
}
