import pyautogui as g
import cv2
import numpy as np
import os
import pytesseract as tes

time_counter = 180

def Kiem_tra_mau():
    total = 100
    current = 30
    if current*100/total <35: #Mau duoi 35% thi tien hanh bom mau
        g.press(2) # dat binh mau tai o so 2

def Buff_ho_tro():
    if time_counter == 0:
        g.press(1)
        g.rightClick(200,200)#buff ho tro cho vdb


while 1:6
    img = g.screenshot()
    bound=(0,0,804,624)
    img = img.crop(bound)
    img = np.array(img)
    
    

    cv2.imshow('Main',img)
    if cv2.waitKey(1) & 0xff == ord('q'):
        break
