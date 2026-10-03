function Show-LyricsOptions($Shared) {
 $form=[Windows.Forms.Form]::new();$form.Text='Текст песни — Music Island';$form.ClientSize=[Drawing.Size]::new(560,365)
 $form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
 $form.BackColor=[Drawing.Color]::FromArgb(16,24,27);$form.ForeColor=[Drawing.Color]::White;$form.Font=[Drawing.Font]::new('Segoe UI',10)
 $label=[Windows.Forms.Label]::new();$label.Text="LRCLIB работает автоматически. Musixmatch — дополнительный источник.`r`nНужен API-ключ с доступом к синхронному тексту.";$label.SetBounds(20,18,520,50);$form.Controls.Add($label)
 $box=[Windows.Forms.TextBox]::new();$box.UseSystemPasswordChar=$true;$box.SetBounds(20,80,350,28);$form.Controls.Add($box)
 $save=[Windows.Forms.Button]::new();$save.Text='Сохранить ключ';$save.SetBounds(380,78,160,32);$form.Controls.Add($save)
 $hint=[Windows.Forms.Label]::new();$hint.Text='Ключ хранится зашифрованным для твоей учётной записи Windows.';$hint.SetBounds(20,118,520,32);$form.Controls.Add($hint)
 $import=[Windows.Forms.Button]::new();$import.Text='Выбрать .lrc для текущей песни';$import.SetBounds(20,163,330,35);$form.Controls.Add($import)
 $remove=[Windows.Forms.Button]::new();$remove.Text='Убрать свой текст';$remove.SetBounds(360,163,180,35);$form.Controls.Add($remove)
 $info=[Windows.Forms.TextBox]::new();$info.Multiline=$true;$info.ReadOnly=$true;$info.ScrollBars='Vertical';$info.SetBounds(20,211,520,132);$info.BackColor=$form.BackColor;$info.ForeColor=$form.ForeColor;$form.Controls.Add($info)
 $current=$Shared.Lyrics;$info.Text="Текущий источник: $($current.Source)`r`n$($Shared.State.Title)`r`n$($current.Copyright)`r`n$($Shared.LyricsError)"
 try {$savedKey=Get-MusixmatchKey;if($savedKey){$hint.Text='Ключ сохранён. Новый заменит его; пустое поле + «Сохранить» удаляет ключ.'};$savedKey=$null}catch{$info.Text=$_.Exception.Message}
 $save.Add_Click({try {Save-MusixmatchKey $box.Text;$box.Clear();$Shared.LyricsRevision=[int]$Shared.LyricsRevision+1;$info.Text='Настройка сохранена. Поиск обновится автоматически.'}catch{$info.Text=$_.Exception.Message}})
 $import.Add_Click({
  $state=$Shared.State;if(-not $state){$info.Text='Сначала включи песню.';return}
  $dialog=[Windows.Forms.OpenFileDialog]::new();$dialog.Filter='Синхронный текст (*.lrc)|*.lrc';$dialog.Title='Текст для: '+$state.Title
  try {if($dialog.ShowDialog() -eq 'OK'){
   if(-not $Shared.State -or $Shared.State.Key -ne $state.Key){$info.Text='Песня сменилась. Выбери файл ещё раз.';return}
   Import-LocalLyrics $dialog.FileName $state;$Shared.LyricsRevision=[int]$Shared.LyricsRevision+1;$info.Text='Текст привязан к этой версии песни. Он имеет приоритет перед поиском.'
  }}catch{$info.Text=$_.Exception.Message}finally{$dialog.Dispose()}
 })
 $remove.Add_Click({try{$state=$Shared.State;if($state){$path=Get-LocalLyricsPath $state;if([IO.File]::Exists($path)){[IO.File]::Move($path,$path+'.removed-'+[DateTime]::UtcNow.Ticks)};$Shared.LyricsRevision=[int]$Shared.LyricsRevision+1;$info.Text='Свой текст отключён. Автопоиск восстановлен; файл сохранён с пометкой .removed.'}}catch{$info.Text=$_.Exception.Message}})
 foreach($button in @($save,$import,$remove)){$button.BackColor=[Drawing.Color]::FromArgb(122,224,195);$button.ForeColor=[Drawing.Color]::FromArgb(16,24,27);$button.FlatStyle='Flat'}
 $box.BackColor=[Drawing.Color]::FromArgb(32,43,47);$box.ForeColor=[Drawing.Color]::White
 try {$null=$form.ShowDialog()}finally{$box.Clear();$form.Dispose()}
}
