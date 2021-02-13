echo "Generating SlnX C# Classes..."
set XSD="C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\xsd.exe"
%XSD% ^
/c /l:CS ^
%CD%\SlnX.xsd ^
/n:Slnx.Generated /o:..\Sources\Slnx\Generated

echo "Generating CsProj C# Classes..."
%XSD% ^
/c /l:CS ^
%CD%\AssemblyReference.xsd ^
/n:Slnx.Generated /o:..\Sources\Slnx\Generated

%XSD% ^
/c /l:CS ^
%CD%\ProjectReference.xsd ^
/n:Slnx.Generated /o:..\Sources\Slnx\Generated