import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-verify-otp',
    templateUrl: './verify-otp.component.html',
    styleUrls: []
})
export class VerifyOtpComponent implements OnInit {
    verifyOtpForm: FormGroup;
    email: string = '';
    error: string = '';
    loading: boolean = false;
    resendCooldown: number = 0;
    private cooldownInterval: any;

    constructor(
        private fb: FormBuilder,
        private authService: AuthService,
        private router: Router
    ) {
        this.verifyOtpForm = this.fb.group({
            otp: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]]
        });

        // Get email from navigation state
        const navigation = this.router.getCurrentNavigation();
        this.email = navigation?.extras?.state?.['email'] || '';
    }

    ngOnInit(): void {
        // Redirect to forgot password if no email is provided
        if (!this.email) {
            this.router.navigate(['/auth/forgot-password']);
        }
    }

    ngOnDestroy(): void {
        if (this.cooldownInterval) {
            clearInterval(this.cooldownInterval);
        }
    }

    onSubmit(): void {
        if (this.verifyOtpForm.valid) {
            this.loading = true;
            this.error = '';

            const otp = this.verifyOtpForm.value.otp;

            this.authService.verifyOtp(this.email, otp).subscribe({
                next: () => {
                    this.loading = false;
                    // Navigate to reset password page
                    this.router.navigate(['/auth/reset-password'], {
                        state: { email: this.email, otp: otp }
                    });
                },
                error: err => {
                    this.loading = false;
                    this.error = err.error?.message || 'Invalid or expired OTP. Please try again.';
                    console.error(err);
                }
            });
        }
    }

    resendOtp(): void {
        if (this.resendCooldown > 0) return;

        this.error = '';
        this.authService.forgotPassword(this.email).subscribe({
            next: () => {
                // Start 60 second cooldown
                this.resendCooldown = 60;
                this.cooldownInterval = setInterval(() => {
                    this.resendCooldown--;
                    if (this.resendCooldown === 0) {
                        clearInterval(this.cooldownInterval);
                    }
                }, 1000);
            },
            error: err => {
                this.error = err.error?.message || 'Failed to resend OTP. Please try again.';
                console.error(err);
            }
        });
    }
}
