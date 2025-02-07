\ #include fixpt-math-lib.fs

#require gpio-simple.fs
#require pwm.fs
#require qei.fs
#require motor.fs
#require mini-imu-9.fs
#require console.fs

: react-init
	motor-init
	i2c-init
	accel-init
	gyro-init
	accel-default
	\ uart-init
;

0 variable accOffsetX
0 variable accOffsetY
0 variable GyZOffset
0 variable vertical
false variable calibrating
false variable calibrated

0,0 2variable robot_angle
0,0 2variable GyZ
0,0 2variable AccAngle
0,996 2constant Gyro_amount
8,0 2constant loop_time

: angle_calc ( -- )
	readAcc drop accOffsetY @ - swap accOffsetX @ - \ AcYc AcXc
	\ Acc_angle = atan2(AcYc, -AcXc)
	s>f dnegate rot s>f 2swap atan2 AccAngle 2!

	readGyro nip nip GyZOffset @ - s>f GyZ 2!

	\ robot_angle += GyZ * loop_time / 1000 / 65.536;
	GyZ 2@ loop_time f* 1000,0 f/ 65,536 f/ robot_angle f+!

	\ robot_angle = robot_angle * Gyro_amount + Acc_angle * (1.0 - Gyro_amount);
	1,0 Gyro_amount f- AccAngle 2@ f* robot_angle 2@ Gyro_amount f* f+ robot_angle 2!

  	\ if (abs(robot_angle) > 6) vertical = false;
  	\ if (abs(robot_angle) < 0.3) vertical = true;
  	robot_angle 2@ dabs 2dup 6,0 d> if false vertical ! 2drop else 0,3 d< if true vertical ! then then
;


0 variable prev1
0 variable prev2

175,0 2variable K1Gain
16,0 2variable K2Gain
0,04 2variable K3Gain
13,0 2variable K4Gain
0,0 2variable gyroZ
0,0 2variable gyroZfilt
0,40 2variable alpha
0,0 2variable motor_speed_enc
0,0 2variable motor_speed
0,0 2variable fpwm

0 variable pwm


: print-robot-angle
	." Acc Angle: " AccAngle 2@ f.r3
	." , robot_angle: " robot_angle 2@ f.r3
	." , vertical: " vertical @ .
	." , raw pwm: " fpwm 2@ f.r3
	." , PWM: " pwm @ . cr
;

$243f6a89 $3 2constant pi
pi 2,0 f* 2constant 2pi

: getVelocity ( -- rps )
	8 get-rpm
	s>f 2pi f*	 	\ convert to radians/min
	60,0 f/ 		\ radians/sec
	get-rpm-dir if dnegate then
;

: calcset-gyrozfilt
	\ gyroZfilt = alpha * gyroZ + (1 - alpha) * gyroZfilt;
	GyZ 2@ 131,0 f/ gyroZ 2!
	1,0 alpha 2@ f- gyroZfilt 2@ f* alpha 2@ gyroZ 2@ f* f+ gyroZfilt 2!
;

: calc-pwm
    \ pwm = constrain(K1Gain * robot_angle + K2Gain * gyroZfilt + K4Gain * motor_speed_enc + K3Gain * motor_speed, -255, 255);
    K1Gain 2@ robot_angle 2@ f*
    K2Gain 2@ gyroZfilt 2@ f* f+
    K4Gain 2@ motor_speed_enc 2@ f* f+
    K3Gain 2@ motor_speed 2@ f* f+
    0,5 f+ 2dup fpwm 2! f>s
    -255 255 constrain pwm !
;

: dopid
	prev1 @ usecdelta 8000 u>= if  \ 8ms looptime
		console
		angle_calc

		vertical @ if
			calcset-gyrozfilt
			calc-pwm

		    getVelocity motor_speed_enc 2!
		    pwm @ set-motor
		    motor_speed_enc 2@ motor_speed f+!

		else
			0 set-motor
			0,0 motor_speed 2!
			0,0 fpwm 2!
			0 pwm !
		then

		usecs prev1 !
	then
;

: angle-setup
	\ init variables
	0 GyZOffset !
	0,0
	1000 0 do
		angle_calc
	    GyZ 2@ f+
	    5 ms
  	loop
  	1000,0 f/ f>s GyZOffset !
  	70 buzz 80 buzz 70 buzz
    ." GyZ offset value: " GyZOffset @ . cr
    print-robot-angle
;

: react
	react-init rpm-init

	angle-setup

	usecs dup prev1 ! prev2 !
	begin
		dopid
		prev2 @ usecdelta 300000 u>= if \ 300ms print time
			print-robot-angle
			usecs prev2 !
		then
		1000 us
	key? until
;
