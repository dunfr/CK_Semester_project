# UI 에셋 편집 기록

built-in imagegen의 `precise-object-edit` 모드로 원본 PNG를 편집했다. 출력은 `Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Refined/`에 저장했다. 실제 알파 배경을 요청했고 결과를 확인한 후 Unity Sprite 영역과 임포트 설정을 조정했다. 원본 PNG는 보존한다.

## 공통 프롬프트

Use case: precise-object-edit. Edit target: existing transparent sci-fi game UI sprite. [아래 개별 요청] Preserve original style, geometry, composition, aspect ratio and tight sprite framing. Do not redesign, add decoration, words or padding. Maintain a real transparent exterior. Output only the single edited sprite.

## 개별 요청과 출력

- `06_Student_Card.png` → `ui_card_blank.png`: Remove only the baked Korean character name, romanized SHIRAYUKI line, and green HP filled bar (leave an empty thin gauge track). Keep portrait, angular white card, cyan/magenta trim, student ID and barcode exactly. Leave name area blank, reconstruct underlying white geometric background.
- `02_Floor_Banner.png` → `ui_floor_blank.png`: Remove all floor/location text including 01F, AREA-03, Korean location, CENTRAL CORRIDOR. Keep the polygon navy banner, cyan edge, pin icon, CCTV icon and all decorative bars. Blank text areas filled seamlessly with original navy translucent panel.
- `01_Location.png` → `ui_location_blank.png`: Remove only Korean location text and CENTRAL CORRIDOR. Keep pin icon and dark navy panel outline. Clean blank text areas.
- `09_Story_Panel.png` → `ui_story_blank.png`: Remove #02, Korean story title, A QUIET ARTIFICIAL CITY, and Korean objective text. Keep STORY tab, horizontal divider, cyan arrow, circular arrow and angular navy panel. Fill vacated text areas with matching underlying navy gradient.
- `10_Minimap.png` → `ui_map_blank.png`: Keep outer circular HUD frame, blue rim, N compass, MAP label, footer academy label and decorative small dashes. Remove 01F floor number, map corridor polygon and its outline, orange objective marker, cyan player arrow and plus zoom buttons. Entire circular interior should be a clean dark navy slightly transparent grid, with no baked map. Preserve exterior transparency.
- `Default_WaveTurnControls.png` → `ui_round_blank.png`: Remove WAVE 1/1 and TURN 01 text only; preserve all blue progress bars and decorations, pause and ESC icons. Blank text areas must be fully transparent, not black rectangles.
- `Turn_MemoryThrowPanel_Unlit.png` → `ui_investment_blank.png`: Remove Korean title and four baked diamond slots. Keep polygon navy panel, cyan/pink trim, divider line and F key icon. Keep vacated title and slot areas seamlessly matching navy panel. No text or slots except F key.

## 적 카드 전체 프롬프트

`Turn_EnemyCard_Blank.png` → `ui_enemycard_blank.png`:

Use case: precise-object-edit. Edit target existing game enemy card sprite. Keep the original angular cyan/red frame, top-right dark-haired girl portrait and small top-right detail T tab. Remove every baked placeholder in the body: Lv. ??, UNKNOWN icon/tag, black horizontal rectangle, Korean weakness/resistance/immunity labels and all symbols/dashes in the lower row. Reconstruct those areas seamlessly as the original clean dark navy gradient panel, no patches or cover boxes. Keep portrait exactly and leave generous blank name/stats areas left of portrait and along bottom. Transparent outside the card. Same original design, aspect ratio, tight framing. Do not redesign or add text.


## 좌클릭 필드 버튼

Built-in imagegen edit. Input: `Default_State/11_Interact_E.png`. Output: `Refined/ui_field_attack_lmb_btn.png`. 원본 보존, Sprite alpha 경계 임포트.

Prompt: Edit this exact existing sci-fi game UI asset. Preserve its circular navy button, cyan luminous outline, white cube icon, style, proportions and transparent background. Only replace the lower right white keyboard key cap with a slightly wider white cap containing a clear small computer mouse pictogram with the LEFT mouse button highlighted dark blue, plus compact exact text 'LMB'. Remove the letter E entirely. No other additions. Keep tightly bounded transparent game sprite.


## 적 카드 샘플 일러스트 제거

Built-in imagegen edit. Input: `Refined/ui_enemycard_blank.png`. Output: `Refined/ui_enemycard_no_portrait_panel.png`. 원본은 보존하며 외곽 투명도를 유지했다.

Prompt: Edit this existing Unity game enemy card UI asset. Remove the entire illustrated anime character, including ghosted silhouette, hair, face, body, and weapon in the upper interior. Seamlessly replace only that artwork with the same dark navy blue subtle gradient already on the left interior. Preserve exact wide card composition, angular cyan/red frame, divider lines, top-right cyan T key badge, empty spaces, and transparent exterior. No new illustration, text, symbols, or objects. Clean blank card interior suitable for displaying runtime enemy data. Retain original aspect ratio and edges.
