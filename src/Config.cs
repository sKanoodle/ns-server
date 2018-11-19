using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NSServer
{
    class Config
    {
        private static string ConfigFile => Path.GetFullPath("config.json");

        private static object Lock = new object();
        private static Config _Instance;
        public static Config Instance
        {
            get
            {
                if (_Instance is null)
                    lock (Lock)
                        if (_Instance is null)
                            _Instance = Load();
                return _Instance;
            }
        }

        private static Config Load()
        {
            var text = File.ReadAllText(ConfigFile);
            var result = JsonConvert.DeserializeObject<Config>(text);
            if (result.Password[0] != '{')
            {
                result.SetPassword(result.Password);
                File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            return result;
        }

        public string ListenIP { get; set; }
        public string ListenPort { get; set; }
        public string ZoneFilePath { get; set; }
        public string EntryToChange { get; set; }
        public string Password { get; set; }
        public string ServerCertificatePath { get; set; }
        public string ServerPrivateKeyPath { get; set; }

        private const int PASSWORD_LENGTH = 20;
        private const int SALT_LENGTH = 16;

        private byte[] Hash
        {
            get => Encoding.ASCII.GetBytes(Password.Substring(1));
            set => Password = "{" + Encoding.ASCII.GetString(value);
        }

        public void SetPassword(string password)
        {
            byte[] salt = new byte[SALT_LENGTH];
            new RNGCryptoServiceProvider().GetBytes(salt);
            var result = new byte[PASSWORD_LENGTH + SALT_LENGTH];
            Array.Copy(HashString(password, salt), 0, result, 0, PASSWORD_LENGTH);
            Array.Copy(salt, 0, result, PASSWORD_LENGTH, SALT_LENGTH);
            Hash = result;
        }

        private byte[] HashString(string password, byte[] salt)
        {
            return new Rfc2898DeriveBytes(password, salt, 10_000).GetBytes(PASSWORD_LENGTH);
        }

        public bool IsPasswordMatch(string password)
        {
            byte[] salt = new byte[SALT_LENGTH];
            Array.Copy(Hash, PASSWORD_LENGTH, salt, 0, SALT_LENGTH);
            byte[] newHash = HashString(password, salt);
            for (int i = 0; i < PASSWORD_LENGTH; i++)
                if (newHash[i] != Hash[i])
                    return false;
            return true;
        }
    }
}
