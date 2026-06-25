using System;
using System.IO;
using System.Xml.Linq;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiStoreProfileService
    {
        private string AiDataDirectory
        {
            get { return Path.Combine(AppPaths.RuntimeRoot, "ai"); }
        }

        private string ProfileFilePath
        {
            get { return Path.Combine(AiDataDirectory, "store-profile.xml"); }
        }

        public AiStoreProfile Load()
        {
            AppPaths.EnsureDirectory(AiDataDirectory);
            if (!File.Exists(ProfileFilePath))
            {
                AppConfig config = AppConfigService.LoadOrCreateDefault();
                return new AiStoreProfile
                {
                    IsInitialized = false,
                    StoreName = config.StoreName,
                    UpdatedAt = DateTime.MinValue
                };
            }

            XDocument document = XDocument.Load(ProfileFilePath);
            XElement root = document.Root;
            return new AiStoreProfile
            {
                IsInitialized = ReadBool(root, "IsInitialized", false),
                StoreName = ReadValue(root, "StoreName", string.Empty),
                StoreLocation = ReadValue(root, "StoreLocation", string.Empty),
                BusinessType = ReadValue(root, "BusinessType", string.Empty),
                MainCustomers = ReadValue(root, "MainCustomers", string.Empty),
                MainProducts = ReadValue(root, "MainProducts", string.Empty),
                OpeningHours = ReadValue(root, "OpeningHours", string.Empty),
                OwnerPreference = ReadValue(root, "OwnerPreference", string.Empty),
                PricingStyle = ReadValue(root, "PricingStyle", string.Empty),
                RestockPreference = ReadValue(root, "RestockPreference", string.Empty),
                CreditPolicy = ReadValue(root, "CreditPolicy", string.Empty),
                Notes = ReadValue(root, "Notes", string.Empty),
                UpdatedAt = ReadDate(root, "UpdatedAt", DateTime.MinValue)
            };
        }

        public void Save(AiStoreProfile profile)
        {
            AppPaths.EnsureDirectory(AiDataDirectory);
            XDocument document = new XDocument(
                new XElement("AiStoreProfile",
                    new XElement("IsInitialized", profile.IsInitialized),
                    new XElement("StoreName", profile.StoreName ?? string.Empty),
                    new XElement("StoreLocation", profile.StoreLocation ?? string.Empty),
                    new XElement("BusinessType", profile.BusinessType ?? string.Empty),
                    new XElement("MainCustomers", profile.MainCustomers ?? string.Empty),
                    new XElement("MainProducts", profile.MainProducts ?? string.Empty),
                    new XElement("OpeningHours", profile.OpeningHours ?? string.Empty),
                    new XElement("OwnerPreference", profile.OwnerPreference ?? string.Empty),
                    new XElement("PricingStyle", profile.PricingStyle ?? string.Empty),
                    new XElement("RestockPreference", profile.RestockPreference ?? string.Empty),
                    new XElement("CreditPolicy", profile.CreditPolicy ?? string.Empty),
                    new XElement("Notes", profile.Notes ?? string.Empty),
                    new XElement("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))));
            document.Save(ProfileFilePath);
        }

        public void Clear()
        {
            if (File.Exists(ProfileFilePath))
            {
                File.Delete(ProfileFilePath);
            }
        }

        private static string ReadValue(XElement root, string name, string defaultValue)
        {
            XElement child = root == null ? null : root.Element(name);
            return child == null ? defaultValue : child.Value;
        }

        private static bool ReadBool(XElement root, string name, bool defaultValue)
        {
            bool result;
            return bool.TryParse(ReadValue(root, name, defaultValue.ToString()), out result) ? result : defaultValue;
        }

        private static DateTime ReadDate(XElement root, string name, DateTime defaultValue)
        {
            DateTime result;
            return DateTime.TryParse(ReadValue(root, name, string.Empty), out result) ? result : defaultValue;
        }
    }
}
