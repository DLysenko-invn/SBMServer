import sys
sys.path.insert(0,'..')

from BLE2TCP import BLE2TCP,Packet
import time

SIF_SERVID = "00001500-2000-1000-8000-cec278b6b50a"
SIF_CHARID_N = "00001501-2000-1000-8000-cec278b6b50a"
SIF_CHARID_RW = "00001502-2000-1000-8000-cec278b6b50a"

SIFRAW_SERVID = "00001600-2000-1000-8000-cec278b6b50a"
SIFRAW_CHARID_N = "00001601-2000-1000-8000-cec278b6b50a"









HOST = '127.0.0.1'
PORT = 65432

# BLE ID
#DEV_ID = "BluetoothLE#BluetoothLE50:76:af:7c:5b:e0-6d:0f:ba:00:02:49" # Qing SB2.0
#DEV_ID = "BluetoothLE#BluetoothLE50:76:af:7c:5b:e0-6d:0f:ba:00:00:4C" # Qing #0x4C

#USB ID
#DEV_ID = "\\\\?\\USB#VID_1915\u0026PID_520F\u0026MI_01#F\u00261450C358\u00260\u00260001#{86e0d1e0-8089-11d0-9ce4-08003e301f73}" # Qing SB2.0





DEV_ID = "\\\\?\\USB#VID_1915\u0026PID_520F\u0026MI_01#6\u00262A820B39\u00260\u00260001#{86e0d1e0-8089-11d0-9ce4-08003e301f73}" # Dmytro
#DEV_ID = "BluetoothLE#BluetoothLE00:28:f8:93:14:84-6d:0f:ba:00:03:1f" #  Dmytro


 






WAIT_SEC = 3
CONNECT_WAIT_SEC = 15
NOTIFY_WAIT_SEC = 10



def PrintBytes(prefix,data):
    print(prefix+''.join('%02X' % x for x in data))


def OnData(devid, subid, data):
    PrintBytes("Notification:",data)

def OnRawData(devid, subid, data):
    PrintBytes("raw:",data)

print("Connecting to the server...")
server = BLE2TCP(HOST, PORT)
rc = server.WaitTillConnected(CONNECT_WAIT_SEC)
if (not rc):
    print("Connection failed")
    exit()
print("Connection to the server OK.")
print("")


print("Connecting to the device...")
device = server.Connect(DEV_ID)
if (device == None):
    print("Connection failed")
    exit()
rc = device.WaitTillConnected(CONNECT_WAIT_SEC)
if (not rc):
    print("Connection failed")
    exit()
print("Connection to the device OK.")
print("")





print("RAW sensor data notification test...")
raw_subid = device.Subscribe(SIFRAW_SERVID,SIFRAW_CHARID_N ,OnRawData)
if (raw_subid==Packet.INDEX_NONE):
    print("Subscribe raw data failed")
    exit()
print("Subscribe to the SIFRAW_SERVID OK.")


print("Label notification test...")
subid = device.Subscribe(SIF_SERVID,SIF_CHARID_N ,OnData)
if (subid==Packet.INDEX_NONE):
    print("Subscribe failed")
    exit()
print("Subscribe to the SIF_SERVID OK.")










print("Wait %i seconds..." % NOTIFY_WAIT_SEC)
time.sleep(NOTIFY_WAIT_SEC)





print("Unsubscribe SIFRAW_SERVID...")
device.Unsubscribe(raw_subid)


print("Unsubscribe SIF_SERVID...")
device.Unsubscribe(subid)






# print("Algo parameters read test...\n")
# somedata = device.Read(SIF_SERVID,SIF_CHARID_RW)
# if (somedata==None):
    # print("Read failed\n")
    # exit()
# PrintBytes("Read:",somedata)






# print("Algo parameters write test...\n")
# somedata = bytearray([0x01,0x02,0x03])
# PrintBytes("Write:",somedata)
# rc = device.Write(SIF_SERVID,SIF_CHARID_RW,somedata)
# if (not rc):
    # print("Write failed\n")
    # exit()







print("Closing...\n")
device.Close()
server.Close()


