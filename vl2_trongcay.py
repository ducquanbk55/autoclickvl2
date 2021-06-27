import pyautogui as g
import cv2

cv2.waitKey(5000)

i = 0
while i < 8:
    print('going')
    g.mouseDown(389,397)
    g.keyDown('down')
    g.keyDown('enter')
    if cv2.waitKey(81000) & 0xff == ord('q'):
        break
    i  = i + 1