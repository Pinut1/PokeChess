- [ ]  김영욱 주간 회의 보고서 중 ‘대용량 에셋이 Git LFS 없이 커밋 되며 레포가 무거워 지는 중’ 내용 확인 작업
    - 확인 작업 및 내용
        
        **원인**
        
        `.gitattributes`에 LFS 필터(`*.fbx` `*.png` `*.psd` 등)는 이미 잘 설정되어 있고 현재 4378개 파일이 LFS로 추적되고 있습니다. 
        문제는 **이 설정이 붙기 전에 이미 일반 커밋으로 들어간 대용량 바이너리들이 히스토리에 그대로 남아있는 것**입니다. 
        LFS 필터는 신규 커밋에만 적용되고 과거 커밋을 소급 변환하지 않기 때문입니다.
        
        **실측 상위 원인**:
        
        - `Assets/Art_VFX/Lana Studio/...` — 구매 VFX 에셋팩(Demo 폴더 포함, 1~2MB급 텍스처/애니메이션 다수)
        - `Assets/_ImportAsset/Maps_Ground/Low/...` — 최대 7.3MB
        - `Assets/Photon/.../PunCockpit-Scene.unity` — Photon 데모 씬 2.1MB (실게임에 불필요)
        - `Assets/Photon/PhotonNetworking-Documentation.chm` — 1.5MB (문서, 불필요)
        
        **해결 방법**
        
        1. 히스토리를 LFS로 재작성
        git lfs migrate import --include="*.png, *.jpg, *.jpeg, *.psd, *.tga, *.tif, *.tiff, *.exr, *.fbx, *.obj, *.blend, *.wav, *.mp3, *.ogg, *.aiff, *.mp4, *.mov" --everything
        `.gitattributes`에 이미 정의된 패턴과 동일하게 지정해 과거 커밋의 blob들을 LFS 포인터로 치환.
        2. 안 쓰는 파일은 아예 삭제 (LFS 이전보다 효율적)
        Photon 데모 씬(`PunCockpit-Scene.unity`), .`chm` 문서처럼 실제 게임에 쓰이지 않는 파일은 `git filter-repo` 또는 BFG Repo-Cleaner로 히스토리에서 완전 제거하는 게 LFS로 옮기는 것보다 나음.
        3. 재작성 후 정리
        git gc --prune=now --aggressive
        로컬 `.git` 용량을 실제로 줄이는 마무리 단계.
        4. **팀 조율 필요** (중요)
        히스토리 재작성은 force-push + 팀 전원 재클론이 필요합니다. 6인 팀이 활발히 작업 중이므로 사전 공지 후 다들 로컬 변경사항 커밋/푸시 완료된 시점에 진행해야 함.
        5. **재발 방지**
        pre-commit hook으로 LFS 미적용 대용량 파일(예: 5MB 이상) 커밋을 차단하면, 앞으로 같은 문제가 다시 쌓이는 걸 막을 수 있습니다.
        
        - **제일 나은 방법**
        
        우선순위로는 2번이 제일 나음.
        실게임에 안 쓰이는 것은 애초에 LFS로 옮길 이유도 없으니, **안쓰는 거 확인 후 삭제 작업**이 가장 이득 대비 리스크가 낮은 조치임.
        
        대신 실제로 게임에 쓰이는 대용량 에셋은 지울 수 없으니 결국 1번(LFS migrate)으로 처리해야하는 경우가 생길 수 있음
        
        - **순서**
        
        2번(삭제 대산 선별) → 1번(나머지는 LFS로) → 3번(gc)을 한 번의 재작성 작업으로 묶어서 진행하는 게 최선임
        
        **일단 클로드가 찾은 필요없는 파일**
        
        ① Demo 폴더 (총 41MB)
        
        | 경로 | 용량 | 비고 |
        | --- | --- | --- |
        | Assets/Photon/PhotonUnityNetworking/Demos | 30M | PUN2 데모(PunCockpit 등)
        게임은 NetworkManager가 PUN2 API만 직접 씀
        즉, 이 데모는 무관 |
        | Assets/_ImportAsset/Maps_Ground/Low Poly Modular | 5.0M | 지형팩 샘플 Scene |
        |  Assets/Photon/PhotonChat/Demos | 2.0M | PhotonChat 자체를 프로젝트에서 안씀(채팅 기능 없음) |
        | Assets/Art_VFX/Click VFX/Demo Scenes | 1.6M | vfx팩 HDRP/URP 데모 씬 |
        | Assets/Art_VFX/Lana Studio/Casual RPG VFX/Demo | 1.2M | vfx팩 테모 (Fire/Slash 등 샘플 씬) |
        | Assets/Photon/PhotonRealtime/Demos | 379K | Photon Realtime 데모 |
        | Assets/Eugatnom/GritlineToonShader/Demo | 654K | 셰이더팩 데모 |
        
        ② 문서 파일 (총 10.8MB)
        
        | 경로 | 용량 |
        | --- | --- |
        | Assets/_ImportAsset/Maps_Ground/Low Poly Modular Terrain Pack/_READ_ME/Documentation.pdf | 7.0M |
        | Assets/Photon/PhotonNetworking-Documentation.chm | 1.5M |
        | Assets/_ImportAsset/Tiles/Gridr/Tutorial/PDF/gridr_tutorial.pdf | 968K |
        | Assets/_ImportAsset/Art_SkyBox/BOXOPHOBIC/Polyverse Skies/Polyverse Skies.pdf | 580K |
        | Assets/_ImportAsset/Tiles/Gridr/gridr_overview.pdf | 516K |
        | Assets/Art_VFX/Matthew Guz/Hits Effects FREE/Documentation/Readme IMPORTANT.pdf | 72K |
        | Assets/Eugatnom/GritlineToonShader/README.pdf | 44K |
        | Assets/Art_VFX/Click VFX/Documentation.pdf | 36K |
        | Assets/_ImportAsset/Art_SkyBox/BOXOPHOBIC/Utils/Utils.pdf | 52K |
        | Assets/_ImportAsset/Maps_Ground/Low Poly Modular Terrain Pack/_READ_ME/License.pdf | 52K |
        
        ⚠️ 현재 워킹트리 크기 기준.
        
        .git이 648MB인 건 이 파일들이 여러 번 재커밋/수정되며 히스토리에 버전마다 쌓였기 때문
        목록 자체는 ‘Demo/Documentation’ 네이밍 컨벤션으로 뽑은 거라 삭제 전에 실제로 씬/프리팹에서 참조 안하는지 GUID 기준으로 한 번 더 확인하는걸 추천.
        
        특히, PhotonUnityNetworking/Demos(제일큼)는 지우기 전에 한 번 열어서 실제 게임 오브젝트가 이 데모 프리팹을 참조 안하는지 확인하는 게 제일 안전.