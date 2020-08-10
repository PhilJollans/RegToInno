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

Coming soon