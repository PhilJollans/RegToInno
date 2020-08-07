//
// This program converts a Windows registry file (.reg) into an INNO SETUP file.
//
// The format of a .reg file is described here:
// https://support.microsoft.com/en-us/help/310516/how-to-add-modify-or-delete-registry-subkeys-and-values-by-using-a-reg
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegToInno
{
  class Program
  {
    static string ShortHive ( string hive )
    {
      switch ( hive )
      {
        case "HKEY_CURRENT_USER":   return "HKCU" ;
        case "HKEY_LOCAL_MACHINE":  return "HKLM" ;
        case "HKEY_CLASSES_ROOT":   return "HKCR" ;
        case "HKEY_USERS":          return "HKU" ;
        case "HKEY_CURRENT_CONFIG": return "HKCC" ;
        default:                    return "" ;
      }
    }

    static string InnoEscape ( string input )
    {
      string output = input.Replace ( "{", "{{" ) ;
      return output ;
    }

    static void Main( string[] args )
    {
      // There must be exactly one parameter
      if ( args.Length != 1 )
      {
        Console.WriteLine ( "Usage:\n" );
        Console.WriteLine ( "RegToInno <reg file>\n" );
        Console.WriteLine ( "  This tool converts the .reg file into an INNO SETUP file\n" );
        Console.WriteLine ( "  containing the same registry entries.\n" );
        Console.WriteLine ( "  The INNO SETUP file will be have the same name plus the \n" );
        Console.WriteLine ( "  additional extension .iss and be in the same directory.\n" );
      }
      else
      {
        // Todo
        // support removing keys
        // [-key]

        // Regular expression to match the registry key.
        // Allow leading spaace, although I don't think there will ever be any.
        Regex reKey = new Regex ( @"^\s*\[(?<hive>[^\\]+)\\(?<key>[^\]]+)\]" ) ;

        // Regular expression to match a string value
        Regex reStringValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*""(?<value>[^""]+)""" ) ;

        // Regular expression to match a DWORD value
        Regex reDwordValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*dword:(?<value>[0-9a-fA-F]+)" ) ;

        // Regular expression to match a REG_EXPAND_SZ value
        Regex reExpandSzValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex\(2\):(?<value>[0-9a-fA-F, ]+)" ) ;

        // Regular expression to match a REG_BINARY value
        Regex reBinaryValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex:(?<value>[0-9a-fA-F, ]+)" ) ;

        string CurrentHive = null ;
        string CurrentKey  = null ;
        string name        = null ;
        string at          = null ;
        string value       = null ;

        using ( var sr = new StreamReader ( args[0] ) )
        {
          using ( var sw = new StreamWriter ( args[0] + ".iss" ) )
          {
            // Write section name to INNO SETUP file
            sw.WriteLine ( $"[Registry]" ) ;

            string line ;
            while ( ( line = sr.ReadLine() ) != null )
            {
              // Check for continuation lines
              // For example:
              //
              //@=hex(2):25,00,73,00,79,00,73,00,74,00,65,00,6d,00,72,00,6f,00,6f,00,74,00,25,\
              //  00,5c,00,73,00,79,00,73,00,74,00,65,00,6d,00,33,00,32,00,5c,00,61,00,74,00,\
              //  6c,00,2e,00,64,00,6c,00,6c,00,00,00
              //
              while ( line.EndsWith(@"\") )
              {
                var nextLine = sr.ReadLine() ;
                if ( nextLine == null )
                  break ;
                else
                  line = line.TrimEnd('\\') + nextLine.TrimStart() ;
              }

              //
              // Check for a registry key
              //
              var matKey = reKey.Match ( line ) ;

              if ( matKey.Success )
              {
                CurrentHive = matKey.Groups["hive"].Value ;
                CurrentKey  = matKey.Groups["key"].Value ;
                continue ;
              }

              //
              // Check for a string value
              //
              var matString = reStringValue.Match ( line ) ;

              if ( matString.Success )
              {
                name  = matString.Groups["name"].Value ;
                at    = matString.Groups["at"].Value ;
                value = matString.Groups["value"].Value ;

                sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: string; ValueData: \"{InnoEscape(value)}\";" ) ;
                continue ;
              }

              //
              // Check for a dword value
              //
              var matDword = reDwordValue.Match ( line ) ;

              if ( matDword.Success )
              {
                name  = matDword.Groups["name"].Value ;
                at    = matDword.Groups["at"].Value ;
                value = matDword.Groups["value"].Value ;

                sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: dword; ValueData: ${InnoEscape(value)};" ) ;
                continue ;
              }

              //
              // Check for a REG_EXPAND_SZ value
              //
              var matExpandSz = reExpandSzValue.Match ( line ) ;

              if ( matExpandSz.Success )
              {
                name  = matExpandSz.Groups["name"].Value ;
                at    = matExpandSz.Groups["at"].Value ;
                value = matExpandSz.Groups["value"].Value ;

                // Remove any spaces from the value
                value = Regex.Replace(value, @"\s", "");

                // Split the value into strings and convert to a byte array
                var bytes       = value.Split(',').Select( x => Convert.ToByte(x,16) ).ToArray() ;
                // Convert the byte array to a string. Remove any null terminator although it is probably harmless.
                var valueString = Encoding.Unicode.GetString(bytes).TrimEnd('\0') ;

                sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: expandsz; ValueData: \"{InnoEscape(valueString)}\";" ) ;
                continue ;
              }

              //
              // Check for a REG_Binary value
              //
              var matBinary = reBinaryValue.Match ( line ) ;

              if ( matBinary.Success )
              {
                name  = matBinary.Groups["name"].Value ;
                at    = matBinary.Groups["at"].Value ;
                value = matBinary.Groups["value"].Value ;

                // Remove any spaces from the value
                value = Regex.Replace(value, @"\s", "");

                // Replace commas with spaces
                value = value.Replace(","," ") ;

                sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: binary; ValueData: \"{value}\"; " ) ;
                continue ;
              }

            }

          }
        }
      }
    }

    private static string CommonPart ( string hive, string key, string name, string at )
    {
      if ( at == "@" )
      {
        return $"Root: {ShortHive(hive)}; Subkey: \"{InnoEscape(key)}\"; Flags: uninsdeletevalue uninsdeletekeyifempty;" ;
      }
      else
      {
        return $"Root: {ShortHive(hive)}; Subkey: \"{InnoEscape(key)}\"; ValueName: {InnoEscape(name)}; Flags: uninsdeletevalue uninsdeletekeyifempty;" ;
      }
    }
  }
}
