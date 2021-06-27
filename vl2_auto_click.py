import typing
import pyautogui as g
from time import sleep
import threading as th
lines_index = 0
counter = 46



def Typing(text):
    g.write(text,0.25)
    g.keyDown('enter')

def Phao_Hoa():
    vl2 =(353, 12)
    vnc =(1393, 70)
    g.press('6')
    i = 18
    while i >0:
        sleep(92)
        g.click(vl2[0], vl2[1])
        g.press('6')
        i = i - 1
        g.click(vnc[0], vnc[1])

def Ky_Nang_Song():
    vl2 =(663, 454)
    g.press('6')
    i = 4
    while i >0:
        sleep(3.9)
        g.click(vl2[0], vl2[1])
        # i = i - 1

def Dcounter():
    global counter
    counter = counter - 1
    if counter < 0:
        counter = 46
    sleep(2)

t = th.Thread(target = Dcounter, args=())

def Swap():
    global lines_index
    vl2 =(353, 12)
    vnc =(1430, 16)
    alls = []
    with open('web.py','r') as o:
        alls = o.read().split('\n')
    
    while 1:
        sleep(1)
        if counter == 0:#Dot phao
            t.stop()
            sleep(3)
            i = 93
            g.click(vl2[0], vl2[1])
            g.press('6')
            print('dot phao')
            t.start()

        else:
            print('Coding')
            g.click(vnc[0], vnc[1])
            Typing(alls[lines_index])
            lines_index = lines_index + 1
            
        print(counter)


sleep(3)
# Phao_Hoa()
Ky_Nang_Song()
# Swap()
# t.start()