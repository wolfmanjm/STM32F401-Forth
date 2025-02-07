#require mini-imu-9.fs

: angle_calc ( ax ay az -- fpangle )
	\ Acc_angle = atan2(AcY, -AcX) * 57.2958;
	drop
	\ convert to FP
	s>f
	rot s>f dnegate
	atan2
;

: invSqrt ( df -- df )
	sqrt 1,0 2swap f/
;

0,0 2variable gx
0,0 2variable gy
0,0 2variable gz
0,0 2variable ax
0,0 2variable ay
0,0 2variable az

1,0 2variable q0
0,0 2variable q1
0,0 2variable q2
0,0 2variable q3

0,0 2variable tx
0,0 2variable ty
0,0 2variable tz

2,0 0,02 f* 2variable twoKp 	\ 2 * proportional gain
2,0 0,0  f* 2variable twoKi		\ 2 * integral gain

0,0 2variable integralFBx
0,0 2variable integralFBy
0,0 2variable integralFBz       \ integral error terms scaled by Ki

1,0 10,0 f/ 2constant INV_SAMPLE_RATE

: normalize_acc	ax 2@ ay 2@ az 2@ normalize3 az 2! ay 2! ax 2! ;

: apply_integral_feedback
	twoKi 2@ tx 2@ f* INV_SAMPLE_RATE f* integralFBx f+!
	twoKi 2@ ty 2@ f* INV_SAMPLE_RATE f* integralFBy f+!
	twoKi 2@ tz 2@ f* INV_SAMPLE_RATE f* integralFBz f+!
	integralFBx 2@ gx f+!
	integralFBy 2@ gy f+!
	integralFBz 2@ gz f+!
;

: compute_feedback
	normalize_acc
		\ Estimated direction of gravity and vector perpendicular to magnetic flux
		\ halfvx = q1 * q3 - q0 * q2;
		\ halfvy = q0 * q1 + q2 * q3;
		\ halfvz = q0 * q0 - 0.5f + q3 * q3;
	q1 2@ q3 2@ f* q0 2@ q2 2@ f* f- tx 2!
	q0 2@ q1 2@ f* q2 2@ q3 2@ f* f+ ty 2!
	q0 2@ **2 0,5 f- q3 2@ **2 f+ tz 2!

		\ Error is sum of cross product between estimated and measured direction of gravity
		\ halfex = (ay * halfvz - az * halfvy);
		\ halfey = (az * halfvx - ax * halfvz);
		\ halfez = (ax * halfvy - ay * halfvx);
	ay 2@ tz 2@ f* az 2@ ty 2@ f* f-
	az 2@ tx 2@ f* ax 2@ tz 2@ f* f-
	ax 2@ ty 2@ f* ay 2@ tx 2@ f* f-
	tz 2! ty 2! tx 2!

		\ Compute and apply integral feedback if enabled
	twoKi 2@ 0,0 d> if
		apply_integral_feedback
	else
		0,0 integralFBx 2!
		0,0 integralFBy 2!
		0,0 integralFBz 2!
	then

	twoKp 2@ tx 2@ f* gx f+!
	twoKp 2@ ty 2@ f* gy f+!
	twoKp 2@ tz 2@ f* gz f+!
;

: mahony ( --  )
		\	if (!((ax == 0.0f) && (ay == 0.0f) && (az == 0.0f))) {
	ax 2@ d0= ay 2@ d0= az 2@ d0= and and 0= if	compute_feedback then


		\ Integrate rate of change of quaternion
	0,5 INV_SAMPLE_RATE f* 2dup gx 2@ f* gx 2!
						   2dup gy 2@ f* gy 2!
	     				 	    gz 2@ f* gz 2!

	q0 2@ tx 2! \ copy q0 to tx
	q1 2@ ty 2!
	q2 2@ tz 2!
	ty 2@ dnegate gx 2@ f* tz 2@ gy 2@ f* f- q3 2@ gz 2@ f* f- q0 f+!
	tx 2@ gx 2@ f* tz 2@ gz 2@ f* f+ q3 2@ gy 2@ f* f- q1 f+!
	tx 2@ gy 2@ f* ty 2@ gz 2@ f* f- q3 2@ gx 2@ f* f+ q2 f+!
	tx 2@ gz 2@ f* ty 2@ gy 2@ f* f+ tz 2@ gx 2@ f* f- q3 f+!

		\ Normalise quaternion
	q0 2@ **2
	q1 2@ **2 f+
	q2 2@ **2 f+
	q3 2@ **2 f+
	invSqrt
	2dup q0 2@ f* q0 2!
	2dup q1 2@ f* q1 2!
	2dup q2 2@ f* q2 2!
	     q3 2@ f* q3 2!
;

: GetEuler ( -- roll pitch yaw )
  \ roll
  \	Atan2(2 * (q0 * q1 + q2 * q3), 1 - 2 * (q1 * q1 + q2 * q2))
  q0 2@ q1 2@ f* q2 2@ q3 2@ f* f+ 2,0 f*
  q1 2@ **2 q2 2@ **2 f+ 2,0 f* 1,0 2swap f-
  atan2

  \ pitch
  \ asin(2 * (q0 * q2 - q3 * q1))
  q0 2@ q2 2@ f* q3 2@ q1 2@ f* f- 2,0 f*
  asin

  \ yaw
  \ Atan2(2 * (q0 * q3 + q1 * q2) , 1 - 2* (q2 * q2 + q3 * q3))
  q0 2@ q3 2@ f* q1 2@ q2 2@ f* f+ 2,0 f*
  q2 2@ **2 q3 2@ **2 f+ 2,0 f* 1,0 2swap f-
  atan2
  2dup d0< if 360,0 f+ then
;

: computeAngles
\   roll = atan2f(q0 * q1 + q2 * q3, 0.5f - q1 * q1 - q2 * q2);
	q0 2@ q1 2@ f* q2 2@ q3 2@ f* f+
	0,5 q1 2@ **2 f- q2 2@ **2 f-
	atan2

\   pitch = asinf(-2.0f * (q1 * q3 - q0 * q2));
	q1 2@ q3 2@ f* q0 2@ q2 2@ f* f- -2,0 f*
	asin

\   yaw = atan2f(q1 * q2 + q0 * q3, 0.5f - q2 * q2 - q3 * q3);
	q1 2@ q2 2@ f* q0 2@ q3 2@ f* f+
	0,5 q2 2@ **2 q3 2@ **2 f- f-
	atan2
\   grav[0] = 2.0f * (q1 * q3 - q0 * q2);
\   grav[1] = 2.0f * (q0 * q1 + q2 * q3);
\   grav[2] = 2.0f * (q1 * q0 - 0.5f + q3 * q3);
;

0,30624 2constant ACC_SCALE_FACTOR \ already /16 in readacc
: get_acc
	readAcc
	s>f az 2!
	s>f ay 2!
	s>f ax 2!
 	ax 2@ ACC_SCALE_FACTOR f* ax 2!
  	ay 2@ ACC_SCALE_FACTOR f* ay 2!
  	az 2@ ACC_SCALE_FACTOR f* az 2!
;

0,0175 2constant GYRO_SCALE_FACTOR
: get_gyro
	readGyro
	s>f gz 2!
	s>f gy 2!
	s>f gx 2!
	\ scale gyro
  	gx 2@ GYRO_SCALE_FACTOR f* gx 2!
  	gy 2@ GYRO_SCALE_FACTOR f* gy 2!
  	gz 2@ GYRO_SCALE_FACTOR f* gz 2!

	\ Convert gyroscope degrees/sec to radians/sec
  	gx 2@ 0,0174533 f* gx 2!
  	gy 2@ 0,0174533 f* gy 2!
  	gz 2@ 0,0174533 f* gz 2!
;

: testimu
	i2c-init accel-init gyro-init
	begin
		get_acc
		get_gyro
		mahony
		\ ." q0 q1 q2 q3: " q0 f? q1 f? q2 f? q3 f? ." , "
		." yaw pitch roll: "
		GetEuler
		\ computeAngles
		f10..3 f10..3 f10..3
		cr
		100 ms
	key? until
;

: testangle
	i2c-init accel-init gyro-init
	begin
		readAcc
		angle_calc
		f.r2
		cr
		100 ms
	key? until
;

: estimate-pitch-roll ( -- pitch roll )
	get_acc
	normalize_acc

		\ pitch = ToDeg(fastAtan2(compass.a.x,sqrt(compass.a.y * compass.a.y + compass.a.z * compass.a.z)));
	ax 2@
	ay 2@ **2 az 2@ **2 f+ sqrt
	atan2

		\ roll = ToDeg(fastAtan2(-1*compass.a.y,sqrt(compass.a.x * compass.a.x + compass.a.z * compass.a.z)));
	ay 2@ dnegate
	ax 2@ **2 az 2@ **2 f+ sqrt
	atan2

	2swap

	az 2@ 0,0 d> if
		ax 2@ 0,0 d> if
			180,0 2swap f-
		else
			-180,0 2swap f-
		then

		2swap

		ay 2@ 0,0 d> if
			-180,0 2swap f-
		else
			180,0 2swap f-
		then
	then
;

: testestimate
	i2c-init accel-init
	begin
		estimate-pitch-roll
		f10..3 f10..3
		cr
		100 ms
	key? until
;
