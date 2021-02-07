echo "Generating C# Classes..."
set XSD="C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\xsd.exe"
%XSD% ^
/c /l:CS ^
%CD%\SlnX.xsd ^
/n:Slnx.Generated /o:..\Sources\Slnx\Generated
