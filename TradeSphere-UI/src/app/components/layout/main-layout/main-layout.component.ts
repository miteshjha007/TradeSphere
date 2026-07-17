import { Component, OnDestroy, OnInit, Renderer2 } from '@angular/core';
import { AuthService } from '../../../modules/auth/services/auth.service';

@Component({
    selector: 'app-main-layout',
    templateUrl: './main-layout.component.html',
    styleUrls: []
})
export class MainLayoutComponent implements OnInit, OnDestroy {
    username: string = 'User';
    isDarkTheme = false;
    private readonly themeStorageKey = 'tradesphere-theme';

    constructor(private authService: AuthService, private renderer: Renderer2) { }

    ngOnInit(): void {
        this.username = this.authService.getUsername();
        const savedTheme = localStorage.getItem(this.themeStorageKey);
        this.isDarkTheme = savedTheme === 'dark';
        this.applyTheme();
    }

    ngOnDestroy(): void {
        this.renderer.removeClass(document.body, 'dark-theme');
    }

    toggleTheme(): void {
        this.isDarkTheme = !this.isDarkTheme;
        localStorage.setItem(this.themeStorageKey, this.isDarkTheme ? 'dark' : 'light');
        this.applyTheme();
    }

    private applyTheme(): void {
        if (this.isDarkTheme) {
            this.renderer.addClass(document.body, 'dark-theme');
            return;
        }

        this.renderer.removeClass(document.body, 'dark-theme');
    }
}
