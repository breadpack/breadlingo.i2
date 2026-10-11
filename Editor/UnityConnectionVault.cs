using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    // Editor-only: no secret is written to Assets, EditorPrefs, manifests or builds.
    public static class UnityConnectionVault
    {
        [StructLayout(LayoutKind.Sequential)] private struct Blob { public int size; public IntPtr data; }
        [DllImport("crypt32.dll", SetLastError=true, CharSet=CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll", SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        public static string Key(string projectPath)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(projectPath)))).Replace("-","").ToLowerInvariant();
        }
        private static string PathFor(string key)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(key??"","^[a-f0-9]{64}$"))throw new ArgumentException("Invalid vault key.");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BreadLingo","Unity",key+".dpapi");
        }
        public static void Save(string key,string value)
        {
            RequireSupportedOS();var path=PathFor(key);Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes=Encoding.UTF8.GetBytes(value);try { var encrypted=Protect(bytes,true);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,encrypted);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }finally{Array.Clear(bytes,0,bytes.Length);}
        }
        public static string Load(string key)
        {
            RequireSupportedOS();var path=PathFor(key);if(!File.Exists(path))return null;
            var bytes=Protect(File.ReadAllBytes(path),false);try{return Encoding.UTF8.GetString(bytes);}finally{Array.Clear(bytes,0,bytes.Length);}
        }
        public static void Delete(string key){var path=PathFor(key);if(File.Exists(path))File.Delete(path);}
        private static void RequireSupportedOS()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)throw new InvalidOperationException("Persistent account pairing currently requires Windows Editor (current-user DPAPI). Use the advanced memory-only connector on other systems.");
        }
        private static byte[] Protect(byte[] bytes,bool encrypt)
        {
            var input=new Blob{size=bytes.Length,data=Marshal.AllocHGlobal(bytes.Length)};Blob output=new Blob();
            try
            {
                Marshal.Copy(bytes,0,input.data,bytes.Length);
                bool ok=encrypt?CryptProtectData(ref input,"BreadLingo Unity",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!ok)throw new InvalidOperationException("The OS could not protect or open this account connection. Reconnect from this Windows account.");
                var result=new byte[output.size];Marshal.Copy(output.data,result,0,result.Length);return result;
            }
            finally {for(int i=0;i<input.size;i++)Marshal.WriteByte(input.data,i,0);Marshal.FreeHGlobal(input.data);if(output.data!=IntPtr.Zero)LocalFree(output.data);}
        }
    }
}
