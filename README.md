
# Cozy Sudoku

Yumuşak, "cozy" temalı sudoku — masaüstü (Windows/macOS), iOS ve Android.

## Modlar
- **Tek Oyuncu** — Kolay / Orta / Zor, 3 ipucu, en iyi süre kaydı.
- **2 Kişi (aynı cihaz)** — Mobilde karşılıklı: üst yarı 180° ters, telefon iki oyuncunun arasında durur. Masaüstünde yan yana.
- **Online** — Photon PUN 2 ile 5 haneli kodlu özel oda (en fazla 4 kişi). Zorluğu kurucu seçer, rakiplerin ilerlemesi canlı görünür, sonda sıralama.

## Kontroller (masaüstü)
| | Tek oyuncu / Online | 2 Kişi – Sol | 2 Kişi – Sağ |
|---|---|---|---|
| Hareket | Oklar / WASD | WASD | Oklar |
| Sayı | 1-9 / Numpad | 1-9 | Numpad 1-9 |
| Not modu | N / Boşluk | Q | Numpad + |
| Sil | Backspace / Del / 0 | E | Numpad 0 |
| Geri al | Z / U | Z | Numpad - |
| İpucu | H | – | – |

## Yapı
Arayüz tamamen koddan kurulur (`Assets/scripts`), sahnelerde yalnızca kamera ve bir denetleyici bulunur:
- `Core/` — tek çözümlü bulmaca üretici (arka plan thread'i), tahta modeli, oturum/ayarlar
- `UI/` — cozy tema, fontlar, buton/tween sistemi, sudoku temalı sahne geçişi, diyaloglar
- `Game/` — tahta görünümü, oyun sahnesi (solo / bölünmüş ekran / online)
- `Menu/` — ana menü, zorluk seçimi, online lobi, ayarlar
- `Net/` — Photon oda yönetimi (RPC yerine oda/oyuncu özellikleri)

## Varlıklar ve lisanslar
- UI sprite'ları: *Cozy UI Pack* — dobo_ui (`Assets/Resources/CozyUI`)
- Fontlar: Nunito, Fredoka — SIL Open Font License (`Assets/ThirdParty/Licenses`)
