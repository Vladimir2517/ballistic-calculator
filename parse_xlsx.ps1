$ErrorActionPreference="Stop"
$xlsx="C:\Projects\ballistic-calculator\input\Таблица_бомбометания.xlsx"
if(-not (Test-Path -LiteralPath $xlsx)){ throw "File not found: $xlsx" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($xlsx)
try {
  $entrySS=$zip.Entries|Where-Object FullName -eq "xl/sharedStrings.xml"|Select-Object -First 1
  $shared=@()
  if($entrySS){
    $sr=[IO.StreamReader]::new($entrySS.Open())
    $ssXml=[xml]$sr.ReadToEnd(); $sr.Close()
    foreach($si in $ssXml.SelectNodes("//*[local-name()=""si""]")){
      $parts=@()
      foreach($t in $si.SelectNodes(".//*[local-name()=""t""]")){ $parts+=[string]$t.InnerText }
      $shared += (($parts -join "") -replace "\s+"," ").Trim()
    }
  }
  $entrySheet=$zip.Entries|Where-Object FullName -eq "xl/worksheets/sheet1.xml"|Select-Object -First 1
  if(-not $entrySheet){ throw "sheet1.xml not found" }
  $sr2=[IO.StreamReader]::new($entrySheet.Open())
  $sheet=[xml]$sr2.ReadToEnd(); $sr2.Close()
  function ColToIndex([string]$letters){ $n=0; foreach($ch in $letters.ToCharArray()){ $n=$n*26+([int][char]$ch-[int][char]"A"+1) }; $n }
  function ParseRef([string]$r){ if($r -match "^([A-Z]+)(\d+)$"){ [pscustomobject]@{Col=ColToIndex $matches[1]; Row=[int]$matches[2]} } else { $null } }
  $out=@()
  foreach($c in $sheet.SelectNodes("//*[local-name()=""sheetData""]//*[local-name()=""c""]")){
    $r=[string]$c.r
    if([string]::IsNullOrWhiteSpace($r)){ continue }
    $p=ParseRef $r
    if(-not $p){ continue }
    if($p.Col -lt 1 -or $p.Col -gt 12 -or $p.Row -lt 1 -or $p.Row -gt 12){ continue }
    $t=[string]$c.t
    $vNode=$c.SelectSingleNode("./*[local-name()=""v""]")
    $fNode=$c.SelectSingleNode("./*[local-name()=""f""]")
    $isNode=$c.SelectSingleNode("./*[local-name()=""is""]")
    $val=""
    if($t -eq "s"){
      $idx=0; if($vNode){ [int]::TryParse($vNode.InnerText,[ref]$idx)|Out-Null }
      if($idx -ge 0 -and $idx -lt $shared.Count){ $val=$shared[$idx] }
    } elseif($t -eq "inlineStr"){
      $parts=@(); if($isNode){ foreach($tn in $isNode.SelectNodes(".//*[local-name()=""t""]")){ $parts+=[string]$tn.InnerText } }
      $val=(($parts -join "") -replace "\s+"," ").Trim()
    } else {
      if($vNode){ $val=[string]$vNode.InnerText }
    }
    $formula=$null; if($fNode){ $formula="="+([string]$fNode.InnerText) }
    if($formula -and -not [string]::IsNullOrWhiteSpace($val)){ $final="$formula => $val" }
    elseif($formula){ $final=$formula } else { $final=$val }
    if(-not [string]::IsNullOrWhiteSpace($final)){
      $out += [pscustomobject]@{Ref=$r; Row=$p.Row; Col=$p.Col; Value=$final}
    }
  }
  $out | Sort-Object Row,Col | ForEach-Object { "{0}={1}" -f $_.Ref,$_.Value }
} finally { $zip.Dispose() }
