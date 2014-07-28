﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;

namespace AppResLibGenerator
{
    public class Generator : Microsoft.Build.Utilities.Task
    {
        public Generator()
        {
            LocaleMappingsFileName = "LocaleMappings.csv";

            Resource100Key = "ApplicationDisplayName";
            Resource101Key = "ApplicationDescription";
            Resource102Key = "ApplicationTileTitle";

            ResourceNotFoundValue = "<RESOURCE NOT FOUND>";
        }

        [Required]
        public string ResXFileName { get; set; }
        [Output]
        public string AppResLibFileName { get; set; }

        public string LocaleMappingsFileName { get; set; }

        public string Resource100Key { get; set; }
        [Output]
        public string Resource100Value { get; set; }

        public string Resource101Key { get; set; }
        [Output]
        public string Resource101Value { get; set; }

        public string Resource102Key { get; set; }
        [Output]
        public string Resource102Value { get; set; }

        public string OutputDirectory { get; set; }

        public string ResourceNotFoundValue { get; set; }

        public void Run()
        {
            if (string.IsNullOrWhiteSpace(ResourceNotFoundValue))
                throw new ArgumentNullException("ResourceNotFoundValue", "Must speicify text to be displayed if resource is not found");

            BuildAppResLibFileName();

            Resource100Value = Resource101Value = Resource102Value = ResourceNotFoundValue;
            ReadResouces();

            GenerateAppResLibFile();
        }

        public override bool Execute()
        {
            try
            {
                Run();

                if (Resource100Value == ResourceNotFoundValue)
                    Log.LogWarning("", "", "", ResXFileName, 1, 1, 1, 1, "Resource string '{0}' was not found", Resource100Key);
                if (Resource101Value == ResourceNotFoundValue)
                    Log.LogWarning("", "", "", ResXFileName, 1, 1, 1, 1, "Resource string '{0}' was not found", Resource101Key);
                if (Resource102Value == ResourceNotFoundValue)
                    Log.LogWarning("", "", "", ResXFileName, 1, 1, 1, 1, "Resource string '{0}' was not found", Resource102Key);
                
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);

                return false;
            }
        }

        void GenerateAppResLibFile()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppResLibGenerator.AppResLib.bin"))
                using (var output = new FileStream(AppResLibFileName, FileMode.Create))
                    stream.CopyTo(output);

            UpdateStringTableResource(AppResLibFileName);
        }

        void UpdateStringTableResource(string path)
        {
            MemoryStream ms = BuildMemoryStream();

            var hResourceUpdate = Interop.BeginUpdateResource(path, false);

            var buffer = ms.GetBuffer();
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var succeeded = Interop.UpdateResource(hResourceUpdate, new IntPtr(6), new IntPtr(7), 5129, handle.AddrOfPinnedObject(), (uint)buffer.Length);
            handle.Free();

            Interop.EndUpdateResource(hResourceUpdate, false);
        }

        MemoryStream BuildMemoryStream()
        {
            var ms = new MemoryStream();

            for (int i = 0; i < 4; i++)
            {
                ms.WriteByte(0);
                ms.WriteByte(0);
            }

            for (int i = 100; i <= 102; i++)
            {
                var value = GetResourceValue(i);

                ms.WriteByte(Convert.ToByte(value.Length));
                ms.WriteByte(0);

                if (value.Length > 0)
                {
                    var valueBytes = Encoding.Unicode.GetBytes(value);
                    ms.Write(valueBytes, 0, valueBytes.Length);
                }
            }

            for (int i = 7; i < 16; i++)
            {
                ms.WriteByte(0);
                ms.WriteByte(0);
            }

            return ms;
        }

        string GetResourceValue(int resourceId)
        {
            switch (resourceId)
            {
                case 100:
                    return Resource100Value;
                case 101:
                    return Resource101Value;
                case 102:
                    return Resource102Value;
                default:
                    throw new NotSupportedException("Resource id not supported: " + resourceId);
            }
        }

        void ReadResouces()
        {
            var resxParser = new ResxParser(ResXFileName);
            if (!string.IsNullOrWhiteSpace(Resource100Key))
                Resource100Value = resxParser.TryGetResourceValue(Resource100Key) ?? ResourceNotFoundValue;
            if (!string.IsNullOrWhiteSpace(Resource101Key))
                Resource101Value = resxParser.TryGetResourceValue(Resource101Key) ?? ResourceNotFoundValue;
            if (!string.IsNullOrWhiteSpace(Resource102Key))
                Resource102Value = resxParser.TryGetResourceValue(Resource102Key) ?? ResourceNotFoundValue;
        }

        void BuildAppResLibFileName()
        {
            var localeMapper = new LocaleMapper();
            localeMapper.Parse(LocaleMappingsFileName);

            var fileName = localeMapper.GetAppResLibFileName(ResXFileName);

            if (string.IsNullOrWhiteSpace(OutputDirectory))
                AppResLibFileName = fileName;
            else
                AppResLibFileName = Path.Combine(OutputDirectory, fileName);
        }
    }
}
