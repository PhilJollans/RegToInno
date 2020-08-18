# RegToInno

RegToInno is a tool to convert a windows registry file (.reg) 
into a script file for use with 
[Inno Setup](https://jrsoftware.org/isinfo.php).

## Usage

The command line syntax for RegToInno is
```
RegToInno <reg file> [-d <directory>] [-r <replacement>]
```
where

| Symbol          | Descrition  | Default
| --------------- | ----------- | ------------
|`<reg file>`    | is the path to the .reg file | None
|`<directory>`   | is the local directory where the COM component was registered | The directory containing the .reg file.
|`<replacement>` | is the string which should replace the local directory | \{app\}

The Inno Setup file is generated in the same directory as the .reg 
file and has the additional extension .iss.

For example, if the input file is
```
c:\MyProject\MyComponent.dll.reg
```
then the output file will be
```
c:\MyProject\MyComponent.dll.reg.iss
```

### Directory name replacement

RegToInno is primarily intended handle .reg files containing the
registration information for COM components. In this case. the .reg
file will contain references to the local directory, where the COM
component was registered.

In the Inno Setup file, the local directory must be replaced with
a path based on an Inno Setup variable, representing the directory
on the target machine where the COM component will be installed.

For example, if a COM component is installed in your main application
directory, this can be represented by the variable `{app}`. If a 
component is installed into a subdirectory named com, then this 
could be represented with `{app}\com`.

You can specify the local directory and the replacement string as
command line parameters.

## Use case COM DLL registration

You can generate the renerate the registration information for a
COM DLL by using 
[RegSpy](https://github.com/PhilJollans/RegSpy)
to extract the registration information from the DLL into a .reg
file, and then using RegToInno to convert the .reg file into an
Inno Setup script.
```
RegSpy c:\MyProject\MyComponent.dll
RegToInno c:\MyProject\MyComponent.dll.reg
```

## Use case COM Visible C# or VB.NET component

You can register a COM Visible .NET comopnent using the utility
RegAsm, with the option /codebase.

If you use the option /regfile, then RegAsm will generate a .reg
file containing the registration information, instead of registering
the component directly.

You can then use RegToInno to convert the .reg file into an Inno
Setup script.
```
RegAsm c:\MyProject\MyComVisible.dll /codebase /regfile:c:\MyProject\MyComVisible.dll.reg
RegToInno c:\MyProject\MyComVisible.dll.reg
```

## Using the Inno Setup Preprocessor

There are two strategies for using RegSpy and RegToInno to generate a setup:

* Run RegSpy (or RegAsm) and RegToInno as part of your build process 
  and include the resulting .iss files in your installation script.

* RegSpy (or RegAsm) and RegToInno via the Inno Setup Preprocessor.

To run the tools via the Inno Setup Preprocesor you can use 
[user defined functions](https://jrsoftware.org/ispphelp/index.php?topic=macros)
based on the following examples.

| :memo: NOTE   |
|:---------------------------|
| I am not an expert on the Inno Setup Proprocessor.<br/>Please let me know if you can simplify or improve these functions. |

### User defined function to install a COM component

In the following example, the function **RegisterCom** runs the tools 
RegSpy and RegToInno and includes the resulting .iss file into the 
installation.

The function **IncludeAndRegisterCom** does the same as **RegisterCom**, 
but in addition it defines a [Files] section and includes the COM component
in the installation.

| :memo: NOTE   |
|:---------------------------|
| In this example, the directory **`{app}\components`** is used as the directory on target system. You will have to change this to specify the right directory for your installation.   |

```
#define RegInnoFile
#sub IncludeRegInnoFile
  #include RegInnoFile
#endsub

#define ComFile
#sub IncludeComFile
[Files]
Source: {#ComFile}; DestDir: {app}\components; Flags: ignoreversion
#endsub

#define RegisterCom(str source)        \
  Exec("RegSpy.exe",source,,,SW_HIDE), \
  Exec("RegToInno.exe",source+".reg -r {app}\components",,,SW_HIDE), \
  RegInnoFile = source+".reg.iss",     \
  IncludeRegInnoFile

#define IncludeAndRegisterCom(str source)        \
  Exec("RegSpy.exe",source,,,SW_HIDE),           \
  Exec("RegToInno.exe",source+".reg -r {app}\components",,,SW_HIDE), \
  RegInnoFile = source+".reg.iss",    \
  IncludeRegInnoFile,                 \
  ComFile = source,                   \
  IncludeComFile

```

To use these functions, you must use the 
[#expr](https://jrsoftware.org/ispphelp/index.php?topic=expr) 
directive, for example

```
#expr IncludeAndRegisterCom("FirstComponent.dll")
#expr IncludeAndRegisterCom("SecondComponent.dll")
#expr IncludeAndRegisterCom("ThirdComponent.dll")
```

**IncludeRegInnoFile** and **IncludeComFile** are helper functions to insert text
into the installation script. There is probably a better way to achieve this.

### User defined function to install a COM-Visible .NET component

In the following example, the function **RegisterInterop** runs the tool
RegAsm to create a registry file with the COM-Visible .NET component and 
then runs RegToInno to convert the .reg file into INNO SETUP syntax.

The function **IncludeAndRegisterInterop** does the same as **RegisterInterop**, 
but in addition it defines a [Files] section and includes the .NET component
in the installation.

| :memo: NOTE   |
|:---------------------------|
| In this example, the directory **`{app}\interop`** is used as the directory on target system. You will have to change this to specify the right directory for your installation.   |

```
#define RegInnoFile
#sub IncludeRegInnoFile
  #include RegInnoFile
#endsub

#define InteropFile
#sub IncludeInteropFile
[Files]
Source: {#InteropFile}; DestDir: {app}\interop; Flags: ignoreversion
#endsub

#define RegisterInterop(str source)   \
  Exec("c:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe",source+" /codebase /regfile:"+source+".reg",,,SW_HIDE),  \
  Exec("RegToInno.exe",source+".reg -r {app}\interop",,,SW_HIDE), \
  RegInnoFile = source+".reg.iss",    \
  IncludeRegInnoFile

#define IncludeAndRegisterInterop(str source)   \
  Exec("c:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe",source+" /codebase /regfile:"+source+".reg",,,SW_HIDE),  \
  Exec("RegToInno.exe",source+".reg -r {app}\interop",,,SW_HIDE), \
  RegInnoFile = source+".reg.iss",    \
  IncludeRegInnoFile,                 \
  InteropFile = source,               \
  IncludeInteropFile
```

To use these functions, you must use the 
[#expr](https://jrsoftware.org/ispphelp/index.php?topic=expr) 
directive, for example

```
#expr IncludeAndRegisterInterop("FirstComVisibleComponent.dll")
#expr IncludeAndRegisterInterop("SecondComVisibleComponent.dll")
#expr IncludeAndRegisterInterop("ThirdComVisibleComponent.dll")
```

**IncludeRegInnoFile** and **IncludeInteropFile** are helper functions to insert text
into the installation script. There is probably a better way to achieve this.

## Download

You can download the 
[executable file here](https://github.com/PhilJollans/RegToInno/raw/master/RegToInno.exe).


