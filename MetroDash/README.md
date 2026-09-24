# Metro Dash — Unity 6

Windows-д зориулсан Subway Surfers маягийн 3 замтай endless runner prototype.
Дүр, хот, галт тэрэг, саад, зоос, дууг кодоор үүсгэдэг. Гаднын asset татахгүй.
Subway Surfers-ийн албан ёсны тоглоом биш; өөрийн нэр, энгийн дүрслэлтэй төсөл.

## Нээж тоглох

1. Unity Hub → **Add → Add project from disk** → энэ `MetroDash` хавтсыг сонгоно.
2. **Unity 6000.6.3f1**-ээр нээнэ. Unity Hub-д нэвтэрсэн, Editor-ийн лиценз идэвхтэй байх хэрэгтэй.
3. Project цонхноос **Assets/Scenes/MetroDash.unity**-г нээнэ.
4. **Play ▶** → **LET'S RUN** дарна. Game цонхон дээр дарж keyboard focus өгнө.

Хот, дүр болон цэс нь Play дарахад үүсдэг тул Edit горимд scene хоосон мэт харагдана.
Scene-г дахин үүсгэх шаардлагатай бол **Metro Dash → Create or reset game scene** ашиглана.

## Удирдлага

| Товч | Үйлдэл |
|---|---|
| A / D эсвэл ← / → | Зүүн, баруун замд шилжих |
| Space / W / ↑ | Үсрэх |
| S / ↓ | Гулгах; агаарт байвал хурдан буух |
| P / Esc | Түр зогсоох / үргэлжлүүлэх |
| Enter / Space | Эхлэх / ялагдсаны дараа дахин эхлэх |
| R | Ялагдсаны дараа дахин эхлэх |
| M | Дуу асаах / унтраах |

Шар намхан хаалтыг үсэрнэ, өндөр хөндлөвчийн доогуур гулгана, галт тэргийг тойрно.
Ногоон shield 9 секундийн дотор нэг мөргөлтөөс хамгаална.
Оноо = явсан метр + зоос × 25. Best score автоматаар хадгалагдана.
Хурд аажмаар нэмэгдэнэ. Саадын мөр бүр дор хаяж нэг нээлттэй замтай.

## Windows .exe гаргах

Unity Hub → Installs → Unity 6000.6.3f1 → Add modules дотор Windows build support байгаа эсэхийг шалгана.
Editor дотор **Metro Dash → Build Windows game** сонгоно.
Төслийн хажууд `MetroDash-Windows/MetroDash.exe` үүснэ.
Тоглоомыг хуулахдаа `.exe`-г дангаар нь биш **MetroDash-Windows хавтсыг бүхлээр** хуулна.

Командын мөрөөр бүтээх бол Windows PowerShell-д:

```powershell
& 'D:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe' -batchmode -quit -projectPath $PWD.Path -executeMethod RunnerBuild.BuildWindows -logFile "$PWD\build.log"
```

Unity өөр газарт суусан бол эхний замыг солино. Төсөл Editor-т нээлттэй үед ижил төслийг batch mode-оор давхар нээж болохгүй.

## Шалгалтын төлөв

- Runtime болон Editor C# кодыг энэ компьютерт суусан Unity **6000.6.3f1**-ийн бодит сангуудтай compile хийж шалгасан.
- Unity batch build нь тусгаарлагдсан орчны кэш бичих эрх болон licensing client-ийн хандалтаас болж дуусаагүй.
- Иймээс Windows executable, Play mode, дүрслэл болон runtime smoke test-ийг одоогоор баталгаажуулаагүй.
- Build хийсний дараа дараах шалгалтыг ажиллуулж болно:

```powershell
& '..\MetroDash-Windows\MetroDash.exe' -batchmode -nographics -runnerSmokeTest -logFile "$PWD\smoke-test.log"
```

Амжилттай үед log-д `METRO_DASH_SMOKE_TEST: PASS` гарна. Энэ нь замын хязгаар,
хөдөлгөөн, үсрэлт, буулт, зоос, гулгалт, shield, мөргөлт, restart болон 100 саадын мөрийн нээлттэй замыг шалгана.
Дараа нь хэвийн цонхтой горимд дүрслэл, дуу, товчлуурыг гараар шалгана.

## Кодын бүтэц

- `Assets/Scripts/MetroDashGame.cs`: тоглоом, зам үүсгэх, дүр, хөдөлгөөн, мөргөлт, цэс, дуу, smoke test.
- `Assets/Editor/RunnerBuild.cs`: scene үүсгэх, Windows build хийх цэс.
- `Assets/Scenes/MetroDash.unity`: эхлэх scene.
- `Assets/Resources/RunnerBase.mat`: built-in Standard материал; runtime дүрслэлийн суурь.

Built-in Render Pipeline, Input Manager ашиглана. Asset Store болон нэмэлт paid package шаардлагагүй.
Дэлгэрэнгүй Unity lifecycle лавлагаа: https://docs.unity3d.com/6000.0/Documentation/Manual/ExecutionOrder.html
