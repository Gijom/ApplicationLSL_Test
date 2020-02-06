# Lab Recorder

Install via: 
https://github.com/labstreaminglayer/App-LabRecorder/releases

## Data format
Recorded data is in .xdf format, you can use the following software to read it:
* Python
  * pyxdf module
* R

You can take a look at https://github.com/xdf-modules for other modules


# Data visualization (Python)
https://gitlab.unige.ch/Marios.Fanourakis/lsl-streams-visualization

# Keyboard/Mouse capture
Source:
https://github.com/labstreaminglayer/App-Input

Release (has only keyboard capture): 
https://github.com/labstreaminglayer/App-Input/releases

# UniGe capture software
source:
https://github.com/Gijom/LabStreamLayer_Applications

Notes: use branch `realsense_fix` for latest

# Bitalino

## HW setup
1. Attach BT dongle to computer
2. navigate to BT settings of your computer and connect the bitalino. Code is 1234

### connections
* A1
  * ECG sensor
* A2
  * EDA sensor
* A3
  * Respiration

### considerations
* check if movement of cables affects signal
  * if it does, how can we mitigate this effect?

## ECG

### tech

Uses *Analog Devices* AD8232 chip in "EXERCISE APPLICATION: HEART RATE MEASURED AT THE HANDS" configuration:
> The overall narrow-band nature of this filter combination distorts the ECG waveform significantly. **Therefore, it is only suitable to determine the heart rate, and not to analyze the ECG signal characteristics.**

See [AD8232 datasheet](https://www.analog.com/media/en/technical-documentation/data-sheets/AD8232.pdf) for more details on the specific chip.

### setup

3 Lead cable in Eindhoven triangle configuration:
* IN+ : left/right collarbone 
* IN- : right/left collarbone
* Ref : iliac crest


## EDA

2-Lead cable with finger straps:

2-Lead cable with gelled electrode pads:


## Respiration

strap sensor just under sternum

## SW setup
```
BitalinoRecorder.exe PlayerID bitalino_device_index
```

# Realsense
## HW setup
1. connect camera to USB3.0 port

## SW setup
```
IntelRealSense-FrameCapture.exe PlayerID
```

### Notes
* resolution for both depth and color is 640x480
* will save image data to rosbag (.bag) file while streaming frame numbers through LSL for synchronization with other data streams
* rosbag file tends to be very large (20GB for 15 minutes), there are ways to compress it after (using ROS compression tool for rosbags)
* To extract the frames as .png from the rosbag, use realsense conversion tool `rs-convert` available after installation of realsense libraries
   Example:
   ```bash
   rs-convert -i P1_1.bag -p ./
   ```
   More options:
   ```bash
   rs-convert -h
   ```
* use ffmpeg to stitch frames together into a video if desired
   ```bash
   ffmpeg -r 30 -i _Color_%*.png -pix_fmt yuv420p -r 30 output.mp4
   ```

#### Installation of realsense libraries
https://github.com/IntelRealSense/librealsense/tree/master/tools/convert

https://github.com/IntelRealSense/librealsense/blob/master/doc/distribution_linux.md

```bash
sudo apt-key adv --keyserver keys.gnupg.net --recv-key F6E65AC044F831AC80A06380C8B3A55A6F3EFCDE || sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-key F6E65AC044F831AC80A06380C8B3A55A6F3EFCDE
sudo add-apt-repository "deb http://realsense-hw-public.s3.amazonaws.com/Debian/apt-repo bionic main" -u
sudo apt install librealsense2-dkms librealsense2-utils
```

# Pressure mat

## HW setup

1. Connect the electronic module to the mat using the pin bars
2. Switch on the electronic unit. The red LED will start blinking

### Via Bluetooth
1. Add BT device with password 1234
2. Identify the COM port assigned to the pressure mat

### Via USB
1. Connect the electronic unit of the mat to the computer's USB port
2. Identify the COM port assigned to the pressure mat

### Notes
Only a part of the pressure mat is covered by the sensors: 
a \~32x32cm area starting from the corner opposite (diagonal) from where the electronic unit connects to the mat. 
Make sure to place the mat such that the part that is equipped with the sensors is properly aligned to the seat. 
Please refer to the [technical documentation](http://sensingtex.com/wp-content/uploads/2018/02/114_WHITE_PAPER_Seating_Mat_English_rev_09.pdf) on the *sensing tex* website for more details.

## SW setup

```
SensingTex-PresureMat.exe PlayerID COM_PORT
```