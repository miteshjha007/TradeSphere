import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-forgot-password',
    templateUrl: './forgot-password.component.html',
    styleUrls: []
})
export class ForgotPasswordComponent {
    forgotPasswordForm: FormGroup;
    error: string = '';
    success: boolean = false;
    loading: boolean = false;

    constructor(
        private fb: FormBuilder,
        private authService: AuthService,
        private router: Router
    ) {
        this.forgotPasswordForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]]
        });
    }

    onSubmit(): void {
        if (this.forgotPasswordForm.valid) {
            this.loading = true;
            this.error = '';

            const email = this.forgotPasswordForm.value.email;

            this.authService.forgotPassword(email).subscribe({
                next: () => {
                    this.loading = false;
                    this.success = true;
                    // Navigate to verify OTP page after 2 seconds
                    setTimeout(() => {
                        this.router.navigate(['/auth/verify-otp'], { state: { email } });
                    }, 2000);
                },
                error: err => {
                    this.loading = false;
                    this.error = err.error?.message || 'Failed to send OTP. Please try again.';
                    console.error(err);
                }
            });
        }
    }
}
