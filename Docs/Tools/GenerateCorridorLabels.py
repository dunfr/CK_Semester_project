from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path('Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Textures/Corridor')
font_path='C:/Windows/Fonts/arialbd.ttf'
regular='C:/Windows/Fonts/arial.ttf'
font=lambda n: ImageFont.truetype(font_path,n)
small=lambda n: ImageFont.truetype(regular,n)
for kind in ['service','caution','deck']:
    im=Image.new('RGBA',(768,256),(0,0,0,0));d=ImageDraw.Draw(im)
    if kind=='service':
        d.polygon([(0,0),(738,0),(768,30),(768,256),(30,256),(0,226)],fill=(23,33,48,245))
        d.rectangle((18,18,750,238),outline=(195,207,220,255),width=3)
        d.rectangle((32,34,45,145),fill=(85,170,226,255))
        d.text((65,25),'SERVICE ACCESS',font=font(47),fill=(226,231,232,255))
        d.text((65,85),'A-17  /  COOLANT SYSTEM',font=small(27),fill=(164,186,206,255))
        d.line((32,150,735,150),fill=(122,142,164,255),width=2)
        for n in range(54):
            width=2+(n*7)%5
            d.rectangle((40+n*7,174,40+n*7+width,218),fill=(202,213,222,255))
        d.text((490,174),'AUTHORIZED\nPERSONNEL ONLY',font=small(18),fill=(202,213,222,255))
    elif kind=='caution':
        d.rectangle((0,0,767,255),fill=(205,156,53,255))
        for n in range(-2,11):d.polygon([(n*96,0),(n*96+30,0),(n*96-55,255),(n*96-85,255)],fill=(36,39,42,255))
        d.rectangle((20,38,748,218),fill=(215,168,61,255))
        d.polygon([(42,193),(112,62),(182,193)],outline=(25,31,38,255),width=8)
        d.text((98,100),'!',font=font(64),fill=(25,31,38,255))
        d.text((210,60),'CAUTION',font=font(58),fill=(25,31,38,255))
        d.text((215,137),'PRESSURIZED LINE  /  04',font=small(26),fill=(25,31,38,255))
    else:
        d.polygon([(0,0),(735,0),(768,33),(768,256),(0,256)],fill=(22,33,51,255))
        d.rectangle((4,4,760,248),outline=(183,201,221,255),width=5)
        d.text((25,25),'01F',font=font(124),fill=(151,189,225,255))
        d.line((280,35,280,210),fill=(164,188,214,255),width=3)
        d.text((307,48),'CENTRAL',font=font(49),fill=(233,235,233,255))
        d.text((307,112),'CORRIDOR',font=font(49),fill=(233,235,233,255))
        d.text((33,194),'AREA-03',font=small(24),fill=(151,189,225,255))
    im.save(root/f'object_corridor_{kind}_label_img.png')
print('3 technical surface labels generated.')
