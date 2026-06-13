import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-reset-password',
    templateUrl: './reset-password.component.html',
    styleUrls: []
})
export class ResetPasswordComponent implements OnInit {
    resetPasswordForm: FormGroup;
    email: string = '';
    otp: string = '';
    error: string = '';
    success: boolean = false;
    loading: boolean = false;

    constructor(
        private fb: FormBuilder,
        private authService: AuthService,
        private router: Router
    ) {
        this.resetPasswordForm = this.fb.group({
            password: ['', [Validators.required, Validators.minLength(6)]],
            confirmPassword: ['', Validators.required]
        }, { validators: this.passwordMatchValidator });

        // Get email and OTP from navigation state
        const navigation = this.router.getCurrentNavigation();
        this.email = navigation?.extras?.state?.['email'] || '';
        this.otp = navigation?.extras?.state?.['otp'] || '';
    }

    ngOnInit(): void {
        // Redirect to forgot password if no email or OTP is provided
        if (!this.email || !this.otp) {
            this.router.navigate(['/auth/forgot-password']);
        }
    }

    passwordMatchValidator(group: FormGroup) {
        const password = group.get('password')?.value;
        const confirmPassword = group.get('confirmPassword')?.value;
        return password === confirmPassword ? null : { passwordMismatch: true };
    }

    onSubmit(): void {
        if (this.resetPasswordForm.valid) {
            this.loading = true;
            this.error = '';

            const newPassword = this.resetPasswordForm.value.password;

            this.authService.resetPassword(this.email, this.otp, newPassword).subscribe({
                next: () => {
                    this.loading = false;
                    this.success = true;
                    // Navigate to login page after 2 seconds
                    setTimeout(() => {
                        this.router.navigate(['/auth/login']);
                    }, 2000);
                },
                error: err => {
                    this.loading = false;
                    this.error = err.error?.message || 'Failed to reset password. Please try again.';
                    console.error(err);
                }
            });
        }
    }
}
