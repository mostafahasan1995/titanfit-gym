// Shared [Code] helpers for server.iss and station.iss (included inside their [Code] section).

function PowerShellPath: String;
begin
  Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
end;

{ Runs one of the setup's PowerShell scripts hidden and returns its exit code (-1 if it did not start).
  Its output (never a secret) is copied into the setup log. }
function RunPowerShellScript(const Script, Arguments: String): Integer;
var
  Params, OutputFile: String;
  Lines: TArrayOfString;
  I, ResultCode: Integer;
begin
  OutputFile := ExpandConstant('{tmp}\powershell-output.txt');
  Params := '/S /C ""' + PowerShellPath + '" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    Script + '" ' + Arguments + ' > "' + OutputFile + '" 2>&1"';
  Log('Running ' + Script + ' ' + Arguments);
  if Exec(ExpandConstant('{cmd}'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
  begin
    Log('PowerShell did not start: ' + SysErrorMessage(ResultCode));
    Result := -1;
  end;
  if LoadStringsFromFile(OutputFile, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      Log('  | ' + Lines[I]);
  DeleteFile(OutputFile);
  Log(Format('PowerShell exit code: %d', [Result]));
end;

{ A JSON string literal. Everything outside printable ASCII is sent as \uXXXX, so the body is plain ASCII. }
function JsonString(const Value: String): String;
var
  I, Code: Integer;
  C: Char;
begin
  Result := '"';
  for I := 1 to Length(Value) do
  begin
    C := Value[I];
    Code := Ord(C);
    if C = '"' then
      Result := Result + '\"'
    else if C = '\' then
      Result := Result + '\\'
    else if (Code < 32) or (Code > 126) then
      Result := Result + '\u' + Format('%.4x', [Code])
    else
      Result := Result + C;
  end;
  Result := Result + '"';
end;

{ The text of a simple "name":"value" field in a JSON response (no unescaping), or ''. }
function JsonTextField(const Json, Name: String): String;
var
  P: Integer;
  Rest: String;
begin
  Result := '';
  P := Pos('"' + Name + '":"', Json);
  if P = 0 then
    exit;
  Rest := Copy(Json, P + Length(Name) + 4, Length(Json));
  P := Pos('"', Rest);
  if P > 0 then
    Result := Copy(Rest, 1, P - 1);
end;

{ Synchronous HTTP request with short timeouts. False when the server could not be reached at all. }
function HttpRequest(const Method, Url, Body: String; var Status: Integer; var Response: String): Boolean;
var
  Request: Variant;
begin
  Result := False;
  Status := 0;
  Response := '';
  try
    Request := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Request.SetTimeouts(5000, 5000, 15000, 30000);
    Request.Open(Method, Url, False);
    if Body <> '' then
      Request.SetRequestHeader('Content-Type', 'application/json; charset=utf-8');
    Request.Send(Body);
    Status := Request.Status;
    Response := Request.ResponseText;
    Result := True;
    Log(Format('%s %s: HTTP %d', [Method, Url, Status]));
  except
    Log(Method + ' ' + Url + ' failed: ' + GetExceptionMessage);
  end;
end;
