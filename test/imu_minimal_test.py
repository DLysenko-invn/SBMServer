
from BLE2TCP import BLE2TCP,Packet
import time

IMU_SERVID    = "00000100-2000-1000-8000-cec278b6b50a"
IMU_CHARID_N  = "00000101-2000-1000-8000-cec278b6b50a"
IMU_CHARID_RW = "00000102-2000-1000-8000-cec278b6b50a"



HOST = '127.0.0.1'
PORT = 65432



#DEV_ID = "SB2S_BA-00-00-14" 
#DEV_ID = "COM10" 
DEV_ID = "COM17" 
 






WAIT_SEC = 3
CONNECT_WAIT_SEC = 15
NOTIFY_WAIT_SEC = 1


def FatalError(text):
    print("ERROR:", text)
    exit()


def PrintBytes(prefix,data):
    print(prefix+''.join('%02X' % x for x in data))


def OnData(devid, subid, data):
    PrintBytes("",data)


print("@ connect server")
server = BLE2TCP(HOST, PORT)
rc = server.WaitTillConnected(CONNECT_WAIT_SEC)
if (not rc):
    FatalError("Connection failed")
print("@ open device")    
device = server.Connect(DEV_ID)
if (device == None):
    FatalError("Connection failed")
rc = device.WaitTillConnected(CONNECT_WAIT_SEC)
if (not rc):
    FatalError("Connection failed")



#somedata = bytearray([0x64,0x00,0x00,0x00,0x00,0x00]) # acc 100Hz, gyr 0Hz, acc 2G, gyr 16dps
#PrintBytes("@ write ",somedata)
#rc = device.Write(IMU_SERVID,IMU_CHARID_RW,somedata)
#if (not rc):
#  FatalError("Write failed\n")


print("@ subscribe")
imu_subid = device.Subscribe(IMU_SERVID,IMU_CHARID_N,OnData)
if (imu_subid==Packet.INDEX_NONE):
    FatalError("Subscribe failed")
print("@ wait %i sec" % NOTIFY_WAIT_SEC)
time.sleep(NOTIFY_WAIT_SEC)
print("@ unsubscrube ")
device.Unsubscribe(imu_subid)




print("@ close")
device.Close()
server.Close()


