\ pwm driver

0 variable PWM_PERIOD_US

: pwm-set-pwm_us ( periodus dutycyleus -- )
	\ base frequency is 1us
	1- TIM3 _tCCR1 !		\ duty cycle
	dup 1- TIM3 _tARR !		\ period
	PWM_PERIOD_US !
;

: pwm-init ( -- )
	\ --- init output PA6 TIM3 CH1
	1 RCC _rAHB1ENR bis!					\ IO port A clock enabled GPIOAEN
	MODE_ALTERNATE 6 PORTA set-moder 		\ PA6 -> mode: alternate function
	SPEED_HIGH 6 PORTA set-opspeed 			\ high speed PA6
	2 6 PORTA set-alternate					\ PA6 -> Alternate function: %0010: AF2 (TIM3)

	\ --- init timer 3
    1 1 lshift RCC _rAPB1ENR bis!			\ TIM3 clock enabled
	1 7 lshift TIM3 _tCR1 ! 				\ Auto-reload preload enable ARPE
	HCLK @ 1000000 / 1- TIM3 _tPSC !		\ 84Mhz hclk prescaler -> 1000000 Hz = 1us
	1000 500 pwm-set-pwm_us					\ 1ms - 500us period - dutycycle as initial value
	1 0 lshift TIM3 _tCCER !				\ CC1E -> OC1 signal is output on the corresponding output pin
	1 3 lshift TIM3 _tCCMR1 bis!			\ OC1PE -> Output compare 1 preload enable
	%110 4 lshift TIM3 _tCCMR1 bis!			\ Output compare 1 mode -> 110: PWM mode 1
	1 TIM3 _tEGR !							\ Update generation UG
	\ 1 TIM3 _tCR1 bis!						\ counter enable
;

: pwm-enable ( flg - )
	1 TIM3 _tCR1
	rot if bis! else bic! then
;

: pwm-pulse_width ( pwus -- )
	1- TIM3 _tCCR1 !
;

\ calculate the pulse width based on the percentage duty cycle
: pwm-duty_cycle ( dc% -- )
	100 PWM_PERIOD_US @ -rot u*/ pwm-pulse_width
;

\ calculate the pulse width based on 0 - 255
: pwm-duty_cycle_255 ( dc -- )
	255 PWM_PERIOD_US @ -rot u*/ pwm-pulse_width
;

: test-pwm ( dc -- )
	pwm-init
	pwm-duty_cycle
	\ pwm-set-pwm_us
	true pwm-enable
	begin key? until
	false pwm-enable
;
