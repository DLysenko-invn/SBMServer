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






DEV_ID = "SB2S_BA-00-00-14" 


 






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


