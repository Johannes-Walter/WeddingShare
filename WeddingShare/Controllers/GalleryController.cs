using System.IO.Compression;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WeddingShare.Attributes;
using WeddingShare.Enums;
using WeddingShare.Extensions;
using WeddingShare.Helpers;
using WeddingShare.Helpers.Database;
using WeddingShare.Helpers.Notifications;
using WeddingShare.Models;
using WeddingShare.Models.Database;

namespace WeddingShare.Controllers
{
    [AllowAnonymous]
    public class GalleryController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfigHelper _config;
        private readonly IDatabaseHelper _database;
        private readonly IFileHelper _fileHelper;
        private readonly IGalleryHelper _gallery;
        private readonly IDeviceDetector _deviceDetector;
        private readonly IImageHelper _imageHelper;
        private readonly INotificationHelper _notificationHelper;
        private readonly IEncryptionHelper _encryptionHelper;
        private readonly Helpers.IUrlHelper _urlHelper;
        private readonly ILogger _logger;
        private readonly IStringLocalizer<Lang.Translations> _localizer;

        private readonly string TempDirectory;
        private readonly string UploadsDirectory;
        private readonly string ThumbnailsDirectory;

        public GalleryController(IWebHostEnvironment hostingEnvironment, IConfigHelper config, IDatabaseHelper database, IFileHelper fileHelper, IGalleryHelper galleryHelper, IDeviceDetector deviceDetector, IImageHelper imageHelper, INotificationHelper notificationHelper, IEncryptionHelper encryptionHelper, Helpers.IUrlHelper urlHelper, ILogger<GalleryController> logger, IStringLocalizer<Lang.Translations> localizer)
        {
            _hostingEnvironment = hostingEnvironment;
            _config = config;
            _database = database;
            _fileHelper = fileHelper;
            _gallery = galleryHelper;
            _deviceDetector = deviceDetector;
            _imageHelper = imageHelper;
            _notificationHelper = notificationHelper;
            _encryptionHelper = encryptionHelper;
            _urlHelper = urlHelper;
            _logger = logger;
            _localizer = localizer;

            TempDirectory = Path.Combine(_hostingEnvironment.WebRootPath, "temp");
            UploadsDirectory = Path.Combine(_hostingEnvironment.WebRootPath, "uploads");
            ThumbnailsDirectory = Path.Combine(_hostingEnvironment.WebRootPath, "thumbnails");
        }

        [HttpPost]
        public IActionResult Login(string id = "default", string? key = null)
        {

            var append = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("id", id)
            };

            if (!string.IsNullOrWhiteSpace(key))
            {
                var enc = _encryptionHelper.IsEncryptionEnabled();
                append.Add(new KeyValuePair<string, string>("key", enc ? _encryptionHelper.Encrypt(key) : key));
                append.Add(new KeyValuePair<string, string>("enc", enc.ToString().ToLower()));
            }

            var redirectUrl = _urlHelper.GenerateFullUrl(HttpContext.Request, "/Gallery", append);

            return new JsonResult(new { success = true, redirectUrl });
        }

        [HttpGet]
        [RequiresSecretKey]
        [AllowGuestCreate]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string id = "default", string? key = null, ViewMode? mode = null, GalleryOrder order = GalleryOrder.UploadedDesc)
        {
            id = (!string.IsNullOrWhiteSpace(id) && !_config.GetOrDefault("Settings:Single_Gallery_Mode", false)) ? id.ToLower() : "default";

            try
            {
                ViewBag.ViewMode = mode ?? (ViewMode)_config.GetOrDefault("Settings:Default_Gallery_View", (int)ViewMode.Default);
            }
            catch
            {
                ViewBag.ViewMode = ViewMode.Default;
            }

            var deviceType = HttpContext.Session.GetString(SessionKey.DeviceType);
            if (string.IsNullOrWhiteSpace(deviceType))
            {
                deviceType = (await _deviceDetector.ParseDeviceType(Request.Headers["User-Agent"].ToString())).ToString();
                HttpContext.Session.SetString(SessionKey.DeviceType, deviceType ?? "Desktop");
            }

            ViewBag.IsMobile = !string.Equals("Desktop", deviceType, StringComparison.OrdinalIgnoreCase);

            var galleryPath = Path.Combine(UploadsDirectory, id);
            _fileHelper.CreateDirectoryIfNotExists(galleryPath);
            _fileHelper.CreateDirectoryIfNotExists(Path.Combine(galleryPath, "Pending"));

            GalleryModel? gallery = await _database.GetGallery(id);
            if (gallery == null)
            {
                gallery = await _database.AddGallery(new GalleryModel()
                {
                    Name = id.ToLower(),
                    SecretKey = key
                });
            }

            if (gallery != null)
            {
                ViewBag.GalleryId = gallery.Name;

                var secretKey = await _gallery.GetSecretKey(gallery.Name);
                ViewBag.SecretKey = secretKey;

                var currentPage = 1;
                try
                {
                    currentPage = int.Parse((Request.Query.ContainsKey("page") && !string.IsNullOrWhiteSpace(Request.Query["page"])) ? Request.Query["page"].ToString().ToLower() : "1");
                }
                catch { }

                var mediaType = mode == ViewMode.Slideshow ? MediaType.Image : MediaType.All;
                var itemsPerPage = _gallery.GetConfig(gallery?.Name, "Gallery_Items_Per_Page", 50);
                var allowedFileTypes = _config.GetOrDefault("Settings:Allowed_File_Types", ".jpg,.jpeg,.png,.mp4,.mov").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var items = (await _database.GetAllGalleryItems(gallery?.Id, GalleryItemState.Approved, mediaType, order, itemsPerPage, currentPage))?.Where(x => allowedFileTypes.Any(y => string.Equals(Path.GetExtension(x.Title).Trim('.'), y.Trim('.'), StringComparison.OrdinalIgnoreCase)));
                
                var isAdmin = User?.Identity != null && User.Identity.IsAuthenticated;

                FileUploader? fileUploader = null;
                if (!string.Equals("All", gallery?.Name, StringComparison.OrdinalIgnoreCase) && (!_gallery.GetConfig(gallery?.Name, "Disable_Upload", false) || isAdmin))
                {
                    var uploadActvated = isAdmin;
                    try
                    {
                        if (!uploadActvated)
                        { 
                            var periods = _gallery.GetConfig(gallery?.Name, "Gallery_Upload_Period", "1970-01-01 00:00")?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (periods != null)
                            { 
                                var now = DateTime.UtcNow;
                                foreach (var period in periods)
                                {
                                    var timeRanges = period?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                    if (timeRanges != null && timeRanges.Length > 0)
                                    {
                                        var startDate = DateTime.Parse(timeRanges[0]).ToUniversalTime();

                                        if (timeRanges.Length == 2)
                                        {
                                            var endDate = DateTime.Parse(timeRanges[1]).ToUniversalTime();
                                            if (now >= startDate && now < endDate)
                                            {
                                                uploadActvated = true;
                                                break;
                                            }
                                        }
                                        else if (timeRanges.Length == 1 && now >= startDate)
                                        {
                                            uploadActvated = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch 
                    {
                        uploadActvated = true;
                    }

                    if (uploadActvated)
                    { 
                        fileUploader = new FileUploader(gallery?.Name ?? "default", secretKey, "/Gallery/UploadImage", _config.GetOrDefault("Settings:Require_Identity_For_Upload", false));
                    }
                }

                var model = new PhotoGallery()
                {
                    GalleryId = gallery?.Id,
                    GalleryName = gallery?.Name,
                    Images = items?.Select(x => new PhotoGalleryImage() 
                    { 
                        Id = x.Id, 
                        GalleryId = x.GalleryId,
                        GalleryName = x.GalleryName,
                        Name = Path.GetFileName(x.Title),
                        UploadedBy = x.UploadedBy,
                        ImagePath = $"/{Path.Combine(UploadsDirectory, x.GalleryName).Remove(_hostingEnvironment.WebRootPath).Replace('\\', '/').TrimStart('/')}/{x.Title}",
                        ThumbnailPath = $"/{ThumbnailsDirectory.Remove(_hostingEnvironment.WebRootPath).Replace('\\', '/').TrimStart('/')}/{Path.GetFileNameWithoutExtension(x.Title)}.webp",
                        MediaType = x.MediaType
                    })?.ToList(),
                    CurrentPage = currentPage,
                    ApprovedCount = gallery?.ApprovedItems ?? 0,
                    PendingCount = gallery?.PendingItems ?? 0,
                    ItemsPerPage = itemsPerPage,
                    FileUploader =  fileUploader,
                    ViewMode = (ViewMode)ViewBag.ViewMode,
                    Pagination = order != GalleryOrder.Random
                };
            
                return View(model);
            }

            return View(new PhotoGallery());
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage()
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;

            try
            {
                string galleryId = (Request?.Form?.FirstOrDefault(x => string.Equals("Id", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString()?.ToLower() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(galleryId))
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Gallery_Id"].Value } });
                }
                
                var gallery = await _database.GetGallery(galleryId);
                if (gallery != null)
                {
                    var secretKey = await _gallery.GetSecretKey(galleryId);
                    string key = (Request?.Form?.FirstOrDefault(x => string.Equals("SecretKey", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(secretKey) && !string.Equals(secretKey, key))
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Secret_Key_Warning"].Value } });
                    }

                    string uploadedBy = HttpContext.Session.GetString(SessionKey.ViewerIdentity) ?? "Anonymous";
                
                    var files = Request?.Form?.Files;
                    if (files != null && files.Count > 0)
                    {
                        var requiresReview = _gallery.GetConfig(galleryId, "Require_Review", true);

                        var uploaded = 0;
                        var errors = new List<string>();
                        foreach (IFormFile file in files)
                        {
                            try
                            {
                                var extension = Path.GetExtension(file.FileName);
                                var maxFilesSize = _config.GetOrDefault("Settings:Max_File_Size_Mb", 10L) * 1000000;
                                var maxGallerySize = _gallery.GetConfig(galleryId, "Max_Gallery_Size_Mb", 1024L) * 1000000;
                                var galleryPath = Path.Combine(UploadsDirectory, gallery.Name);

                                var allowedFileTypes = _config.GetOrDefault("Settings:Allowed_File_Types", ".jpg,.jpeg,.png,.mp4,.mov").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                if (!allowedFileTypes.Any(x => string.Equals(x.Trim('.'), extension.Trim('.'), StringComparison.OrdinalIgnoreCase)))
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Invalid_File_Type"].Value}");
                                }
                                else if (file.Length > maxFilesSize)
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Max_File_Size"].Value} {maxFilesSize} bytes");
                                }
                                else if ((_fileHelper.GetDirectorySize(galleryPath) + file.Length) > maxGallerySize)
                                {
                                    errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Gallery_Full"].Value} {maxGallerySize} bytes");
                                }
                                else
                                {
                                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                                    galleryPath = requiresReview ? Path.Combine(galleryPath, "Pending") : galleryPath;
                                    
                                    _fileHelper.CreateDirectoryIfNotExists(galleryPath);

                                    var filePath = Path.Combine(galleryPath, fileName);
                                    if (!string.IsNullOrWhiteSpace(filePath))
                                    {
                                        await _fileHelper.SaveFile(file, filePath, FileMode.Create);

                                        var checksum = await _fileHelper.GetChecksum(filePath);
                                        if (_gallery.GetConfig(galleryId, "Gallery_Prevent_Duplicates", true) && string.IsNullOrWhiteSpace(checksum) || await _database.GetGalleryItemByChecksum(gallery.Id, checksum) != null)
                                        {
                                            errors.Add($"{_localizer["File_Upload_Failed"].Value}. {_localizer["Duplicate_Item_Detected"].Value}");
                                            _fileHelper.DeleteFileIfExists(filePath);
                                        }
                                        else
                                        { 
                                            _fileHelper.CreateDirectoryIfNotExists(ThumbnailsDirectory);
                                            await _imageHelper.GenerateThumbnail(filePath, Path.Combine(ThumbnailsDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.webp"), _config.GetOrDefault("Settings:Thumbnail_Size", 720));

                                            var item = await _database.AddGalleryItem(new GalleryItemModel()
                                            {
                                                GalleryId = gallery.Id,
                                                Title = fileName,
                                                UploadedBy = uploadedBy,
                                                Checksum = checksum,
                                                MediaType = _imageHelper.GetMediaType(filePath),
                                                State = requiresReview ? GalleryItemState.Pending : GalleryItemState.Approved
                                            });

                                            if (item?.Id > 0)
                                            { 
                                                uploaded++;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"{_localizer["Save_To_Gallery_Failed"].Value} - {ex?.Message}");
                            }
                        }

						Response.StatusCode = (int)HttpStatusCode.OK;

						return Json(new { success = uploaded > 0, uploaded, uploadedBy, requiresReview, errors });
                    }
                    else
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["No_Files_For_Upload"].Value } });
                    }
                }
                else
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Gallery_Does_Not_Exist"].Value } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Image_Upload_Failed"].Value} - {ex?.Message}");
            }

            return Json(new { success = false, uploaded = 0 });
        }

        [HttpPost]
        public async Task<IActionResult> UploadCompleted()
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;

            try
            {
                string galleryId = (Request?.Form?.FirstOrDefault(x => string.Equals("Id", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString()?.ToLower() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(galleryId))
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Gallery_Id"].Value } });
                }

                var gallery = await _database.GetGallery(galleryId);
                if (gallery != null)
                {
                    var secretKey = await _gallery.GetSecretKey(galleryId);
                    string key = (Request?.Form?.FirstOrDefault(x => string.Equals("SecretKey", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(secretKey) && !string.Equals(secretKey, key))
                    {
                        return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Invalid_Secret_Key_Warning"].Value } });
                    }

                    string uploadedBy = HttpContext.Session.GetString(SessionKey.ViewerIdentity) ?? "Anonymous";
                        
                    var requiresReview = _gallery.GetConfig(galleryId, "Require_Review", true);

                    int uploaded = int.Parse((Request?.Form?.FirstOrDefault(x => string.Equals("Count", x.Key, StringComparison.OrdinalIgnoreCase)).Value)?.ToString() ?? "0");
                    if (uploaded > 0 && requiresReview && _config.GetOrDefault("Notifications:Alerts:Pending_Review", true))
                    {
                        await _notificationHelper.Send(_localizer["New_Items_Pending_Review"].Value, $"{uploaded} new item(s) have been uploaded to gallery '{gallery.Name}' by '{(!string.IsNullOrWhiteSpace(uploadedBy) ? uploadedBy : "Anonymous")}' and are awaiting your review.", _urlHelper.GenerateBaseUrl(HttpContext?.Request, "/Admin"));
                    }

                    Response.StatusCode = (int)HttpStatusCode.OK;

                    return Json(new { success = true, uploaded, uploadedBy, requiresReview });
                }
                else
                {
                    return Json(new { success = false, uploaded = 0, errors = new List<string>() { _localizer["Gallery_Does_Not_Exist"].Value } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Image_Upload_Failed"].Value} - {ex?.Message}");
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> DownloadGallery(int id)
        {
            try
            {
                var gallery = await _database.GetGallery(id);
                if (gallery != null)
                {
                    if (!_gallery.GetConfig(gallery.Name, "Disable_Download", false) || (User?.Identity != null && User.Identity.IsAuthenticated))
                    {
                        var galleryDir = id > 0 ? Path.Combine(UploadsDirectory, gallery.Name) : UploadsDirectory;
                        if (_fileHelper.DirectoryExists(galleryDir))
                        {
                            _fileHelper.CreateDirectoryIfNotExists(TempDirectory);

                            var tempZipFile = Path.Combine(TempDirectory, $"{gallery.Name}-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.zip");
                            ZipFile.CreateFromDirectory(galleryDir, tempZipFile, CompressionLevel.Optimal, false);

                            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                            {
                                using (var fs = new FileStream(tempZipFile, FileMode.Open, FileAccess.ReadWrite))
                                using (var archive = new ZipArchive(fs, ZipArchiveMode.Update, false))
                                {
                                    foreach (var entry in archive.Entries.Where(x => x.FullName.StartsWith("Pending/", StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith("Rejected/", StringComparison.OrdinalIgnoreCase)).ToList())
                                    {
                                        entry.Delete();
                                    }
                                }
                            }

                            return Json(new { success = true, filename = $"/temp/{Path.GetFileName(tempZipFile)}" });
                        }
                    }
                    else
                    {
                        return Json(new { success = false, message = _localizer["Download_Gallery_Not_Allowed"].Value });
                    }
                }
                else
                {
                    return Json(new { success = false, message = _localizer["Failed_Download_Gallery"].Value });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_localizer["Failed_Download_Gallery"].Value} - {ex?.Message}");
            }

            return Json(new { success = false });
        }
    }
}