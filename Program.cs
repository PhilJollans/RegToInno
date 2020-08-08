﻿//
// This program converts a Windows registry file (.reg) into an INNO SETUP file.
//
// The format of a .reg file is described here:
// https://support.microsoft.com/en-us/help/310516/how-to-add-modify-or-delete-registry-subkeys-and-values-by-using-a-reg
//
// This is also interesting
// https://docs.microsoft.com/en-us/windows/win32/sysinfo/registry-value-types
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

    static string RegFileUnescape ( string input )
    {
      string output = input.Replace ( @"\\", @"\" ) ;
      return output ;
    }

    static void Usage()
    {
      Console.WriteLine (
@"
This tool converts the .reg file into an INNO SETUP file containing the same registry entries.
The INNO SETUP file will be have the same name plus the additional extension .iss and be in
the same directory.

Usage:
RegToInno <reg file> [-d <directory>] [-r <replacement>]

<reg file>      Name of the file to be converted.

<directory>     Local diretory name which must be replaced in the INNO SETUP file.
                Default value is the directory containing <reg file>

<replacement>   String to be used in the INNO SETUP file in place of <directory>.
                Defult value {app}
" ) ;
      Environment.Exit(1) ;
    }

    static void Main( string[] args )
    {
      if ( args.Length == 0 )
        Usage() ;

      string argFile        = null ;
      string argDirectory   = null ;
      string argReplacement = null ;
      string argSwitch      = null ;

      // Get parameters from the command line
      for ( int i = 0 ; i < args.Length ; i++ )
      {
        string param = args[i] ;
        if ( param.StartsWith("-") )
        {
          argSwitch = param ;
        }
        else
        {
          if ( argSwitch == "-d" )
            argDirectory = param ;
          else if ( argSwitch == "-r" )
            argReplacement = param ;
          else
            argFile = param ;

          argSwitch = null ;
        }
      }

      // The filename is mandatory
      if ( string.IsNullOrEmpty ( argFile ) )
        Usage() ;

      // Set defaults for the other parameters
      if ( string.IsNullOrEmpty ( argDirectory ) )
        argDirectory = Path.GetDirectoryName ( argFile ) ;

      if ( string.IsNullOrEmpty ( argReplacement ) )
        argReplacement = "{app}" ;




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

      // The regular expressions for REG_BINARY, REG_EXPAND_SZ, REG_QWORD and REG_MULTI_SZ
      // are almost exactly the same. Obviosuly we could restructure the program to use
      // a single regular expression, but actually I'm fairly happy treating each type
      // separately.

      // Regular expression to match a REG_BINARY value
      Regex reBinaryValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex:(?<value>[0-9a-fA-F, ]+)" ) ;

      // Regular expression to match a REG_EXPAND_SZ value
      Regex reExpandSzValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex\(2\):(?<value>[0-9a-fA-F, ]+)" ) ;

      // Regular expression to match a REG_QWORD value
      Regex reQWordValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex\(b\):(?<value>[0-9a-fA-F, ]+)" ) ;

      // Regular expression to match a REG_MULTI_SZ value
      Regex reMultiSzValue = new Regex ( @"^\s*(""(?<name>[^""]+)""|(?<at>@))\s*=\s*hex\(7\):(?<value>[0-9a-fA-F, ]+)" ) ;

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

              // Process escape characters used in a reg file, specifically replace \\ with \
              value = RegFileUnescape ( value ) ;

              // Insert escape characters required for INNO SETUP, specifically {{ for {, before ...
              value = InnoEscape ( value ) ;

              // ... replacing the directory on the local machine, with the replacement string used by INNO SETUP
              // which will almost certainly contain { characters which must not be escaped.
              value = value.Replace ( argDirectory, argReplacement ) ;

              sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: string; ValueData: \"{value}\";" ) ;
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

              sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: dword; ValueData: ${value};" ) ;
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

              // Process escape characters used in a reg file, specifically replace \\ with \
              valueString = RegFileUnescape ( valueString ) ;

              // Insert escape characters required for INNO SETUP, specifically {{ for {, before ...
              valueString = InnoEscape ( valueString ) ;

              // ... replacing the directory on the local machine, with the replacement string used by INNO SETUP
              // which will almost certainly contain { characters which must not be escaped.
              valueString = valueString.Replace ( argDirectory, argReplacement ) ;

              sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: expandsz; ValueData: \"{valueString}\";" ) ;
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

            //
            // Check for a REG_QWORD value
            //
            var matQWord = reQWordValue.Match ( line ) ;

            if ( matQWord.Success )
            {
              name  = matQWord.Groups["name"].Value ;
              at    = matQWord.Groups["at"].Value ;
              value = matQWord.Groups["value"].Value ;

              // Remove any spaces from the value
              value = Regex.Replace(value, @"\s", "");

              // Split the value into strings and convert to a byte array
              var bytes       = value.Split(',').Select( x => Convert.ToByte(x,16) ).ToArray() ;
              // Convert the byte array to a QWORD
              var qword = BitConverter.ToUInt64 ( bytes, 0 ) ;

              sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: qword; ValueData: \"{qword}\";" ) ;
              continue ;
            }

            //
            // Check for a REG_MULTI_SZ value
            //
            var matMultiSz = reMultiSzValue.Match ( line ) ;

            if ( matMultiSz.Success )
            {
              name  = matMultiSz.Groups["name"].Value ;
              at    = matMultiSz.Groups["at"].Value ;
              value = matMultiSz.Groups["value"].Value ;

              // Remove any spaces from the value
              value = Regex.Replace(value, @"\s", "");

              // Split the value into strings and convert to a byte array
              var bytes       = value.Split(',').Select( x => Convert.ToByte(x,16) ).ToArray() ;
              // Convert the byte array to a string. Remove any null terminator although it is probably harmless.
              var valueString = Encoding.Unicode.GetString(bytes).TrimEnd('\0') ;

              // Escape any curly brackets in the strings ...
              valueString = InnoEscape ( valueString ) ;
              // ... BEFORE replacing embedded nulls with the Inno Setup syntax {break}
              valueString = valueString.Replace ( "\0", "{break}" ) ;

              sw.WriteLine ( $"{CommonPart(CurrentHive,CurrentKey,name,at)} ValueType: multisz; ValueData: \"{valueString}\";" ) ;
              continue ;
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
