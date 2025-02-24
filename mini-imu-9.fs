#require i2c.fs

\ constants for the IMU
$6B constant GYRO_ADDR   \ L3GD20 gyro
$19 constant ACCEL_ADDR  \ LSM303DLHC_DEVICE accel
$1E constant MAG_ADDR    \ LSM303DLHC_DEVICE magno

$20 constant LSM303_CTRL_REG1_A
$21 constant LSM303_CTRL_REG2_A
$22 constant LSM303_CTRL_REG3_A
$23 constant LSM303_CTRL_REG4_A
$24 constant LSM303_CTRL_REG5_A
$25 constant LSM303_CTRL_REG6_A

$00 constant LSM303_CRA_REG_M
$01 constant LSM303_CRB_REG_M
$02 constant LSM303_MR_REG_M
$03 constant LSM303_OUT_X_H_M

$28 constant LSM303_OUT_X_L_A
$29 constant LSM303_OUT_X_H_A
$2A constant LSM303_OUT_Y_L_A
$2B constant LSM303_OUT_Y_H_A
$2C constant LSM303_OUT_Z_L_A
$2D constant LSM303_OUT_Z_H_A


$31 constant LSM303_TEMP_OUT_H_M
$32 constant LSM303_TEMP_OUT_L_M

$0F constant L3G_WHOAMI
$26 constant L3G_OUT_TEMP
$20 constant L3G_CTRL_REG1
$21 constant L3G_CTRL_REG2
$22 constant L3G_CTRL_REG3
$23 constant L3G_CTRL_REG4
$24 constant L3G_CTRL_REG5

$28 constant L3G_OUT_X_L
$29 constant L3G_OUT_X_H
$2A constant L3G_OUT_Y_L
$2B constant L3G_OUT_Y_H
$2C constant L3G_OUT_Z_L
$2D constant L3G_OUT_Z_H

\ buffer to store multibyte i2c traffic
8 buffer: mimu-i2cbuf
: mimu-i2cbuf! ( c # -- ) mimu-i2cbuf + c! ;
: mimu-i2cbuf@ ( # -- u ) mimu-i2cbuf + c@ ;

\ convert 16bit signed to 32bit signed
: 16>32 ( s -- s ) $8000 xor $8000 - ;

: mimu-get-reg ( reg addr -- val )
    i2c-getreg
;

\ returns requested registers in mimu-i2cbuf
: mimu-get-regs ( n reg addr -- errflg )
	i2c-start if 2drop drop true exit then
	>r
	r@ i2c-send1 i2c-restart or if rdrop drop true exit then
	mimu-i2cbuf r> i2c-readbuf if true exit then
	false
;

: mimu-writereg ( val reg addr -- )
	i2c-send2stop if ." write Reg failed" exit then
;

: whoami.
	." L3G:"
	L3G_WHOAMI GYRO_ADDR mimu-get-reg dup $8000 and if ." Error getting reg" cr exit then
	dup hex.
	$D4 = if ."  ID ok" else ."  unknown ID" then
	cr
;

: readtemp.
	L3G_OUT_TEMP GYRO_ADDR mimu-get-reg dup $8000 and if ." Error getting reg" cr exit then
	dup hex. . cr
;


: gyro-init ( -- errflg )
	GYRO_ADDR i2c-deviceready? not if ." Gyro not found" true exit then

  	$0F L3G_CTRL_REG1 GYRO_ADDR mimu-writereg 	\ enable all, 100 hz
	$00 L3G_CTRL_REG2 GYRO_ADDR mimu-writereg 	\ high pass filter
	$00 L3G_CTRL_REG3 GYRO_ADDR mimu-writereg
	$20 L3G_CTRL_REG4 GYRO_ADDR mimu-writereg 	\ 2000 dps
	\ $10 L3G_CTRL_REG4 GYRO_ADDR mimu-writereg 	\ 500 dps, update after read
	$00 L3G_CTRL_REG5 GYRO_ADDR mimu-writereg
	false
;

: gyro-default
	$0F L3G_CTRL_REG1 GYRO_ADDR mimu-writereg 	\ enable all, 95 hz
	$00 L3G_CTRL_REG4 GYRO_ADDR mimu-writereg
;

: readGyro ( -- gx gy gz )
	\ read the 6 registers into the mimu-i2cbuf
	6 L3G_OUT_X_L $80 or GYRO_ADDR mimu-get-regs if 0 0 0 ." ERROR reading regs" exit then
	\ result is in mimu-i2cbuf
	\ convert to gx gy gz
	6 0 do
		i mimu-i2cbuf@    \ l
		i 1+ mimu-i2cbuf@ \ h
		8 lshift or
		16>32
	2 +loop
;

: writeAccReg ( val reg -- )
	ACCEL_ADDR mimu-writereg
;

: writeMagReg ( val reg -- )
	MAG_ADDR mimu-writereg
;

: accel-init
  \ Enable Accelerometer
  \ 0x27 = 0b00100111
  \ Normal power mode, all axes enabled
  \ $77 LSM303_CTRL_REG1_A writeAccReg \ 400Hz
  $57 LSM303_CTRL_REG1_A writeAccReg \ normal 100Hz all axes enabled
  $10 LSM303_CTRL_REG4_A writeAccReg \ Continuous update little endian +/- 4g
;

: accel-default
  $47 LSM303_CTRL_REG1_A writeAccReg \ 50Hz
  $00 LSM303_CTRL_REG4_A writeAccReg \ +/-2g 1mg/LSB
;

: mag-init
  \ Enable Magnetometer
  \ 0x00 = 0b00000000
  \ Continuous conversion mode
  $00 LSM303_MR_REG_M writeMagReg
  $98 LSM303_CRA_REG_M writeMagReg \ 75Hz, enable temp sensor
  $A0 LSM303_CRB_REG_M writeMagReg \ gain 2.5, xy 670, z 600
;

: mag-default
  $00 LSM303_MR_REG_M writeMagReg
  $08 LSM303_CRA_REG_M writeMagReg
  $20 LSM303_CRB_REG_M writeMagReg
;

: lsm303-init
	accel-init
	mag-init
;

: readAcc ( -- ax ay az )
	\ read the 6 registers
	6 LSM303_OUT_X_L_A $80 or ACCEL_ADDR mimu-get-regs if 0 0 0 ." ERROR reading regs" exit then

	\ convert to ax ay az
	\ ax = ((int16_t)(xha << 8 | xla)) >> 4;
	6 0 do
		i mimu-i2cbuf@    \ l
		i 1+ mimu-i2cbuf@ \ h
		8 lshift or
		16>32
		\ (12-bit resolution, left-aligned).
		4 arshift
	2 +loop

	\ ax ay az
;

: readMag ( -- mx my mz )
	\ read the 6 registers
	6 LSM303_OUT_X_H_M MAG_ADDR mimu-get-regs if 0 0 0 ." ERROR reading regs" exit then

	\ convert to mx mz my
	6 0 do
		i mimu-i2cbuf@    	\ h
		i 1+ mimu-i2cbuf@ 	\ l
		swap			\ l h
		8 lshift or
		16>32
	2 +loop
	swap 			\ mx my mz
;

: n.8 DUP ABS 0 <# #s ROT SIGN #> dup 8 swap - spaces type ;

: testacc
	lsm303-init
	begin
		readAcc
		-rot swap
		." ax: " n.8
		." , ay: " n.8
		." , az: " n.8
		cr
		100 ms
	key? until
;

: testmag
	lsm303-init
	begin
		readMag
		-rot swap
		." mx: " n.8
		." , my: " n.8
		." , mz: " n.8
		cr
		100 ms
	key? until
;

: testgyro
	i2c-init
	gyro-init if exit then
	begin
		readGyro
		-rot swap
		." gx: " n.8
		." , gy: " n.8
		." , gz: " n.8
		cr
		100 ms
	key? until
;
