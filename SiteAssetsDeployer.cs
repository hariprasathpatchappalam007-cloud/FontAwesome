using System;
using System.IO;
using Falcon_SP.CsomDeploy.Helpers;
using Microsoft.SharePoint.Client;

namespace Falcon_SP.CsomDeploy.Deployer
{
    /// <summary>
    /// Recursively uploads a local folder to a SharePoint document library folder
    /// using the CSOM FileCreationInformation API.
    /// Idempotent: existing files are overwritten (CheckInRequired=false).
    /// </summary>
    public class SiteAssetsDeployer
    {
        private readonly ClientContext _ctx;
        private readonly string _targetLibrary;
        private readonly string _targetFolder;
        private int _uploaded;
        private int _failed;

        public SiteAssetsDeployer(ClientContext ctx, string targetLibrary, string targetFolder)
        {
            if (ctx == null)           throw new ArgumentNullException("ctx");
            if (targetLibrary == null) throw new ArgumentNullException("targetLibrary");
            if (targetFolder == null)  throw new ArgumentNullException("targetFolder");
            _ctx           = ctx;
            _targetLibrary = targetLibrary;
            _targetFolder  = targetFolder;
        }

        /// <summary>
        /// Uploads all files under <paramref name="localRoot"/> to the configured SP folder.
        /// </summary>
        public void Deploy(string localRoot)
        {
            if (!Directory.Exists(localRoot))
                throw new DirectoryNotFoundException("Source folder not found: " + localRoot);

            // Resolve SP web server-relative URL root (e.g. /sites/PET)
            _ctx.Load(_ctx.Web, w => w.ServerRelativeUrl, w => w.Url);
            _ctx.ExecuteQuery();
            string webUrl = _ctx.Web.ServerRelativeUrl.TrimEnd('/');

            // Ensure the root target folder exists
            string rootFolderUrl = webUrl + "/" + _targetLibrary + "/" + _targetFolder;
            EnsureFolder(_ctx.Web, _targetLibrary, _targetFolder);

            DeployLogger.Info("Deploying: " + localRoot);
            DeployLogger.Info("Target   : " + rootFolderUrl);

            UploadDirectory(localRoot, localRoot, webUrl + "/" + _targetLibrary + "/" + _targetFolder);

            DeployLogger.Success("Deployment complete. Uploaded: " + _uploaded + "  Failed: " + _failed);
        }

        // ---- Private Helpers ------------------------------------------------

        private void UploadDirectory(string rootPath, string currentDir, string spFolderUrl)
        {
            // Upload files in the current directory
            foreach (string filePath in Directory.GetFiles(currentDir))
            {
                UploadFile(filePath, spFolderUrl);
            }

            // Recurse into sub-directories
            foreach (string subDir in Directory.GetDirectories(currentDir))
            {
                string folderName = Path.GetFileName(subDir);
                string subSpUrl   = spFolderUrl + "/" + folderName;

                // Ensure sub-folder exists in SP
                EnsureFolderByUrl(subSpUrl);

                UploadDirectory(rootPath, subDir, subSpUrl);
            }
        }

        private void UploadFile(string localFilePath, string spFolderUrl)
        {
            string fileName = Path.GetFileName(localFilePath);
            string spFileUrl = spFolderUrl + "/" + fileName;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(localFilePath);

                var fileInfo = new FileCreationInformation
                {
                    Content     = fileBytes,
                    Url         = spFileUrl,
                    Overwrite   = true
                };

                Folder spFolder = _ctx.Web.GetFolderByServerRelativeUrl(spFolderUrl);
                spFolder.Files.Add(fileInfo);
                _ctx.ExecuteQuery();

                _uploaded++;
                DeployLogger.Success("  Uploaded: " + spFileUrl);
            }
            catch (Exception ex)
            {
                _failed++;
                DeployLogger.Error("  Failed  : " + spFileUrl, ex);
            }
        }

        /// <summary>
        /// Ensures a folder exists directly under a library using the list/folder API.
        /// </summary>
        private void EnsureFolder(Web web, string libraryTitle, string folderName)
        {
            List library = web.Lists.GetByTitle(libraryTitle);
            _ctx.Load(library.RootFolder);
            _ctx.ExecuteQuery();

            string rootUrl = library.RootFolder.ServerRelativeUrl.TrimEnd('/');
            EnsureFolderByUrl(rootUrl + "/" + folderName);
        }

        private void EnsureFolderByUrl(string serverRelativeUrl)
        {
            // Normalise – strip trailing slash
            serverRelativeUrl = serverRelativeUrl.TrimEnd('/');

            // ── Check whether the folder already exists ───────────────────
            // GetFolderByServerRelativeUrl returns an object that may be
            // "not found"; we must catch the exception OR check Exists.
            // Using try/catch is more reliable across SP versions.
            bool exists = false;
            try
            {
                Folder existing = _ctx.Web.GetFolderByServerRelativeUrl(serverRelativeUrl);
                _ctx.Load(existing, f => f.Exists);
                _ctx.ExecuteQuery();
                exists = existing.Exists;
            }
            catch { /* folder probably doesn't exist – fall through */ }

            if (exists) return;

            // ── Create via the PARENT folder's Folders collection ─────────
            // _ctx.Web.Folders.Add(url) only works for web-root folders.
            // For nested library sub-folders we MUST use parentFolder.Folders.Add(childName).
            int lastSlash = serverRelativeUrl.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                DeployLogger.Warning("  Cannot determine parent for folder: " + serverRelativeUrl);
                return;
            }

            string parentUrl  = serverRelativeUrl.Substring(0, lastSlash);
            string folderName = serverRelativeUrl.Substring(lastSlash + 1);

            try
            {
                Folder parent = _ctx.Web.GetFolderByServerRelativeUrl(parentUrl);
                parent.Folders.Add(folderName);
                _ctx.ExecuteQuery();
                DeployLogger.Info("  Created folder: " + serverRelativeUrl);
            }
            catch (Exception ex2)
            {
                DeployLogger.Warning("  Could not create folder: " + serverRelativeUrl + " – " + ex2.Message);
            }
        }
    }
}
