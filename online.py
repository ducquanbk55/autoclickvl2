import pyautogui as g
import cv2

g.FAILSAFE = False
def Coding():
    with open('Main.cs','r') as o:
        alls = o.read().split('\n')
        for i in range(len(alls)):
            g.write(alls[i],0.25)
            if i != 0 and i%10 == 0:
                cv2.waitKey(5000)
            g.keyDown('enter')
 
while 1:
    # g.moveTo(500,500)
    print('going')
    # g.moveTo(600,500)
    if cv2.waitKey(5000) & 0xff == ord('q'):
        break
    Coding()